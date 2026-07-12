using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain;
using Njord.Ingest;

namespace Njord.Pipeline;

public sealed class PipelineActor : ReceiveActor, IWithStash
{
    private readonly NjordOptions _options;
    private readonly ResolvedParameterSet _parameters;
    private readonly IOpenMeteoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PipelineActor> _logger;
    private readonly ActorRegistry _registry;

    private ISinkRef<WeightedTarget>? _sinkRef;
    private ISourceRef<FetchOutcome.Success>? _sourceRef;

    public IStash Stash { get; set; } = null!;

    private sealed record StreamRefsMaterialized(
        ISinkRef<WeightedTarget> SinkRef,
        ISourceRef<FetchOutcome.Success> SourceRef);

    public PipelineActor(
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters,
        IOpenMeteoClient client,
        TimeProvider timeProvider,
        ILogger<PipelineActor> logger,
        ActorRegistry registry)
    {
        _options = options.Value;
        _parameters = parameters;
        _client = client;
        _timeProvider = timeProvider;
        _logger = logger;
        _registry = registry;

        Initializing();
    }

    protected override void PreStart()
    {
        MaterializePipeline();
    }

    private void Initializing()
    {
        Receive<StreamRefsMaterialized>(msg =>
        {
            _sinkRef = msg.SinkRef;
            _sourceRef = msg.SourceRef;
            _logger.LogInformation("Pipeline graph materialized — ready to accept producers and consumers");
            Become(Ready);
            Stash.UnstashAll();
        });
        ReceiveAny(_ => Stash.Stash());
    }

    private void Ready()
    {
        Receive<RequestPipelineSink>(_ =>
        {
            if (_sinkRef is not null)
                Sender.Tell(new PipelineSinkResponse(_sinkRef));
        });
        Receive<RequestPipelineSource>(_ =>
        {
            if (_sourceRef is not null)
                Sender.Tell(new PipelineSourceResponse(_sourceRef));
        });
    }

    private void MaterializePipeline()
    {
        var mat = Context.Materializer();
        var budget = _options.EffectiveBudget;
        var budgetPerMinute = (int)(budget.RequestsPerMinute * 0.8);
        var schedulerActor = _registry.Get<SchedulerActor>();

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<WeightedTarget>(perProducerBufferSize: 16)
            .PreMaterialize(mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<FetchOutcome.Success>(bufferSize: 256)
            .PreMaterialize(mat);

        mergeHubSource
            .Throttle(budgetPerMinute, TimeSpan.FromMinutes(1), budgetPerMinute,
                element => element.Weight, ThrottleMode.Shaping)
            .Via(FetchStage.Create(_client, _timeProvider))
            .Collect(outcome => outcome is FetchOutcome.Success s ? s : null!)
            .Where(s => s is not null)
            .RunWith(broadcastHubSink, mat);

        broadcastHubSource
            .Select(success => new HashResult(
                success.Forecast.Location,
                success.Forecast.Model.Id,
                ForecastDataHash.Compute(success.Forecast, _timeProvider)))
            .Ask<Ack>(schedulerActor, TimeSpan.FromSeconds(5))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(
                _ => Akka.Streams.Supervision.Directive.Resume))
            .To(Sink.Ignore<Ack>())
            .Run(mat);

        var self = Self;
        var sinkRefTask = StreamRefs.SinkRef<WeightedTarget>()
            .To(mergeHubSink)
            .Run(mat);

        var sourceRefTask = broadcastHubSource
            .RunWith(StreamRefs.SourceRef<FetchOutcome.Success>(), mat);

        Task.WhenAll(sinkRefTask, sourceRefTask)
            .ContinueWith(_ => new StreamRefsMaterialized(sinkRefTask.Result, sourceRefTask.Result))
            .PipeTo(self);
    }
}
