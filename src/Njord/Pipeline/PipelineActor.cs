using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Domain.Weather;
using Njord.Ingest;
using Servus.Akka;

namespace Njord.Pipeline;

public sealed class PipelineActor : ReceiveActor, IWithStash
{
    private readonly IOpenMeteoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly IBudgetGate<WeightedTarget> _budgetGate;
    private readonly ILogger<PipelineActor> _logger;

    private Source<FetchOutcome, NotUsed>? _broadcastHubSource;
    private ISinkRef<WeightedTarget>? _warmSinkRef;
    private Sink<WeightedTarget, NotUsed>? _mergeHubSink;
    private IMaterializer? _mat;

    public IStash Stash { get; set; } = null!;

    private sealed record PipelineReady;
    private sealed record SinkRefWarmed(ISinkRef<WeightedTarget> SinkRef);

    public PipelineActor(
        IOpenMeteoClient client,
        TimeProvider timeProvider,
        IBudgetGate<WeightedTarget> budgetGate,
        ILogger<PipelineActor> logger)
    {
        _client = client;
        _timeProvider = timeProvider;
        _budgetGate = budgetGate;
        _logger = logger;

        Initializing();
    }

    protected override void PreStart()
    {
        MaterializePipeline();
    }

    private void Initializing()
    {
        Receive<PipelineReady>(_ =>
        {
            _logger.LogInformation("Pipeline graph materialized - ready to accept producers and consumers");
            Become(Ready);
            Stash.UnstashAll();
        });
        ReceiveAny(_ => Stash.Stash());
    }

    private void Ready()
    {
        Receive<SinkRefWarmed>(msg =>
        {
            _warmSinkRef = msg.SinkRef;
            Stash.UnstashAll();
        });
        Receive<RequestPipelineSink>(_ =>
        {
            if (_warmSinkRef is not null)
            {
                Sender.Tell(new PipelineSinkResponse(_warmSinkRef));
                _warmSinkRef = null;
                PreWarmSinkRef();
            }
            else
            {
                Stash.Stash();
            }
        });
        Receive<RequestPipelineSource>(_ =>
        {
            var sourceRef = _broadcastHubSource!
                .RunWith(StreamRefs.SourceRef<FetchOutcome>(), _mat!);
            sourceRef.PipeTo(Sender, Self,
                sr => new PipelineSourceResponse(sr),
                ex =>
                {
                    _logger.LogError(ex, "Failed to create SourceRef");
                    return null!;
                });
        });
    }

    private void PreWarmSinkRef()
    {
        StreamRefs.SinkRef<WeightedTarget>()
            .To(_mergeHubSink!)
            .Run(_mat!)
            .PipeTo(Self,
                success: sr => new SinkRefWarmed(sr),
                failure: ex =>
                {
                    _logger.LogError(ex, "Failed to pre-warm SinkRef");
                    return null!;
                });
    }

    private void MaterializePipeline()
    {
        _mat = Context.Materializer();
        var schedulerActor = Context.GetActor<SchedulerActor>();

        var (mergeHubSink, mergeHubSource) = MergeHub.Source<WeightedTarget>(perProducerBufferSize: 16)
            .PreMaterialize(_mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<FetchOutcome>(bufferSize: 2)
            .PreMaterialize(_mat);

        mergeHubSource
            .Via(new BudgetThrottleStage<WeightedTarget>(_budgetGate))
            .SelectAsyncUnordered(2, async target =>
            {
                var outcome = await _client.FetchAsync(target.Location, target.Model, target.Cycle, CancellationToken.None);
                return outcome;
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(_ => Akka.Streams.Supervision.Directive.Resume))
            .Buffer(32, OverflowStrategy.Backpressure)
            .To(broadcastHubSink)
            .Run(_mat);

        broadcastHubSource
            .Collect(outcome => outcome is FetchOutcome.Success, outcome => (FetchOutcome.Success)outcome)
            .Select(success => new HashResult(
                success.Forecast.Location,
                success.Forecast.Model.Id,
                ForecastDataHash.Compute(success.Forecast, _timeProvider)))
            .Ask<Ack>(schedulerActor, TimeSpan.FromSeconds(5))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(_ => Akka.Streams.Supervision.Directive.Resume))
            .To(Sink.Ignore<Ack>())
            .Run(_mat);

        _mergeHubSink = mergeHubSink;
        _broadcastHubSource = broadcastHubSource;

        PreWarmSinkRef();
        Self.Tell(new PipelineReady());
    }
}
