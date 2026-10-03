using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Actors;
using Njord.Domain.Weather;
using Njord.Ingest;
using Njord.Messages.Common;
using Njord.Messages.Pipeline;
using Servus.Akka;

namespace Njord.Pipeline;

public sealed class PipelineActor : ReceiveActor, IWithStash
{
    private readonly IOpenMeteoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly IBudgetGate<WeightedTarget> _budgetGate;
    private ILoggingAdapter _log = null!;

    private Sink<WeightedTarget, NotUsed>? _mergeHubSink;
    private Source<FetchOutcome, NotUsed>? _broadcastHubSource;
    private IMaterializer? _mat;
    private UniqueKillSwitch? _killSwitch;
    private Task[] _completions = [];

    public IStash Stash { get; set; } = null!;

    private sealed record PipelineReady;
    private sealed record SchedulerResolved(IActorRef Ref);

    public PipelineActor(
        IOpenMeteoClient client,
        TimeProvider timeProvider,
        IBudgetGate<WeightedTarget> budgetGate)
    {
        _client = client;
        _timeProvider = timeProvider;
        _budgetGate = budgetGate;

        Initializing();
    }

    protected override void PreStart()
    {
        _log = Context.GetLogger();
        _mat = Context.Materializer();
        Context.GetActorAsync<ISchedulerActor>()
            .PipeTo(Self, success: r => new SchedulerResolved(r));
    }

    private void Initializing()
    {
        Receive<SchedulerResolved>(msg => MaterializePipeline(msg.Ref));
        Receive<PipelineReady>(_ =>
        {
            _log.Info("Pipeline graph materialized - ready to accept producers and consumers");
            Become(Ready);
            Stash.UnstashAll();
        });
        ReceiveAny(_ => Stash.Stash());
    }

    private void Ready()
    {
        Receive<StopStreams>(_ =>
        {
            _killSwitch?.Shutdown();
            Task.WhenAll(_completions)
                .PipeTo(Sender, Self,
                    success: () => new StreamsStopped(),
                    failure: ex => new StreamsStopFailed(ex));
        });
        Receive<RequestPipelineSink>(msg =>
        {
            StreamRefs.SinkRef<WeightedTarget>()
                .To(_mergeHubSink!)
                .Run(_mat!)
                .PipeTo(Sender, Self,
                    sr => new PipelineSinkResponse(msg.RequestId, sr),
                    ex =>
                    {
                        _log.Error(ex, "Failed to create SinkRef");
                        return new PipelineSinkFailed(msg.RequestId, ex);
                    });
        });
        Receive<RequestPipelineSource>(msg =>
        {
            _broadcastHubSource!
                .RunWith(StreamRefs.SourceRef<FetchOutcome>(), _mat!)
                .PipeTo(Sender, Self,
                    sr => new PipelineSourceResponse(msg.RequestId, sr),
                    ex =>
                    {
                        _log.Error(ex, "Failed to create SourceRef");
                        return new PipelineSourceFailed(msg.RequestId, ex);
                    });
        });
    }

    private void MaterializePipeline(IActorRef schedulerActor)
    {
        var (mergeHubSink, mergeHubSource) = MergeHub.Source<WeightedTarget>(perProducerBufferSize: 16)
            .PreMaterialize(_mat);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<FetchOutcome>(bufferSize: 2)
            .PreMaterialize(_mat);

        var (killSwitch, fetchCompletion) = mergeHubSource
            .ViaMaterialized(KillSwitches.Single<WeightedTarget>(), Keep.Right)
            .Via(new BudgetThrottleStage<WeightedTarget>(_budgetGate))
            .Log("pipeline-fetch-in", t => $"{t.Location.Name}/{t.Model.Id}", _log)
            .SelectAsyncUnordered(2, async target =>
            {
                var outcome = await _client.FetchAsync(target.Location, target.Model, target.Cycle, CancellationToken.None);
                return outcome;
            })
            .Log("pipeline-fetch-out", o => o switch
            {
                FetchOutcome.Success s => $"OK {s.Forecast.Location}/{s.Forecast.Model.Id}",
                FetchOutcome.Failure f => $"FAIL {f.Location}/{f.Model.Id} {f.Reason}",
                _ => "?",
            }, _log)
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .Buffer(32, OverflowStrategy.Backpressure)
            .WatchTermination((killSwitch, completion) => (killSwitch, completion))
            .To(broadcastHubSink)
            .Run(_mat);

        var hashCompletion = broadcastHubSource
            .Collect(outcome => outcome is FetchOutcome.Success, outcome => (FetchOutcome.Success)outcome)
            .Select(success => new HashResult(
                success.Forecast.Location,
                success.Forecast.Model.Id,
                ForecastDataHash.Compute(success.Forecast, _timeProvider)))
            .Log("pipeline-hash", h => $"{h.Location}/{h.ModelId} hash={h.Hash}", _log)
            .Ask<Ack>(schedulerActor, TimeSpan.FromSeconds(5))
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .ToMaterialized(Sink.Ignore<Ack>(), Keep.Right)
            .Run(_mat);

        _killSwitch = killSwitch;
        _completions = [fetchCompletion, hashCompletion];
        _mergeHubSink = mergeHubSink;
        _broadcastHubSource = broadcastHubSource;

        Self.Tell(new PipelineReady());
    }
}
