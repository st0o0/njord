using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Ingest;
using Servus.Akka;

namespace Njord.Pipeline;

public sealed class PipelineActor : ReceiveActor, IWithStash
{
    private readonly NjordOptions _options;
    private readonly IOpenMeteoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PipelineActor> _logger;

    private ISinkRef<WeightedTarget>? _sinkRef;
    private ISourceRef<FetchOutcome>? _sourceRef;

    public IStash Stash { get; set; } = null!;

    private sealed record StreamRefsMaterialized(
        ISinkRef<WeightedTarget> SinkRef,
        ISourceRef<FetchOutcome> SourceRef);

    public PipelineActor(
        IOptions<NjordOptions> options,
        IOpenMeteoClient client,
        TimeProvider timeProvider,
        ILogger<PipelineActor> logger)
    {
        _options = options.Value;
        _client = client;
        _timeProvider = timeProvider;
        _logger = logger;

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
            {
                Sender.Tell(new PipelineSinkResponse(_sinkRef));
            }
        });
        Receive<RequestPipelineSource>(_ =>
        {
            if (_sourceRef is not null)
            {
                Sender.Tell(new PipelineSourceResponse(_sourceRef));
            }
        });
    }

    private void MaterializePipeline()
    {
        var mat = Context.Materializer();
        var budget = _options.EffectiveBudget;
        var budgetPerMinute = (int)(budget.RequestsPerMinute * 0.8);
        var schedulerActor = Context.GetActor<SchedulerActor>();

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<WeightedTarget>(perProducerBufferSize: 16)
            .PreMaterialize(mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<FetchOutcome>(bufferSize: 256)
            .PreMaterialize(mat);

        mergeHubSource
            .Throttle(budgetPerMinute, TimeSpan.FromMinutes(1), budgetPerMinute,
                element => element.Weight, ThrottleMode.Shaping)
            .SelectAsyncUnordered(8, async target =>
                await _client.FetchAsync(target.Location, target.Model, target.Cycle, CancellationToken.None))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(_ => Akka.Streams.Supervision.Directive.Resume))
            .To(broadcastHubSink)
            .Run(mat);

        broadcastHubSource
            .Collect(outcome => outcome is FetchOutcome.Success, outcome => (FetchOutcome.Success)outcome)
            .Select(success => new HashResult(
                success.Forecast.Location,
                success.Forecast.Model.Id,
                ForecastDataHash.Compute(success.Forecast, _timeProvider)))
            .Ask<Ack>(schedulerActor, TimeSpan.FromSeconds(5))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(_ => Akka.Streams.Supervision.Directive.Resume))
            .To(Sink.Ignore<Ack>())
            .Run(mat);

        var self = Self;
        var sinkRefTask = StreamRefs.SinkRef<WeightedTarget>()
            .To(mergeHubSink)
            .Run(mat);

        var sourceRefTask = broadcastHubSource
            .RunWith(StreamRefs.SourceRef<FetchOutcome>(), mat);

        Task.WhenAll(sinkRefTask, sourceRefTask)
            .ContinueWith(_ => new StreamRefsMaterialized(sinkRefTask.Result, sourceRefTask.Result))
            .PipeTo(self);
    }
}