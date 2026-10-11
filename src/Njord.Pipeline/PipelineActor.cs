using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Alerts;
using Njord.Compute.Analysis;
using Njord.Core.Actors;
using Njord.Core.Egress;
using Njord.Core.Ingest;
using Njord.Derived;
using Njord.Indices;
using Njord.Messages.Pipeline;
using Njord.Sensors;
using Njord.Weather;
using Servus.Akka;
using TrendAnalyzer = Njord.Trends.TrendAnalyzer;

namespace Njord.Pipeline;

public sealed class PipelineActor : ReceiveActor
{
    private readonly IOpenMeteoClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly IBudgetGate<WeightedTarget> _budgetGate;
    private readonly ConsensusSnapshotFactory _consensusFactory;
    private readonly AlertAnalyzer _alerts;
    private readonly DerivedAnalyzer _derived;
    private readonly TrendAnalyzer _trends;
    private readonly IndexAnalyzer _indices;
    private readonly IActorRef _scheduler;
    private ILoggingAdapter _log = null!;

    private Source<PipelineEvent, NotUsed>? _pipelineOutputSource;
    private IMaterializer? _mat;
    private ISourceQueueWithComplete<WeightedTarget>? _queue;
    private UniqueKillSwitch? _killSwitch;
    private IActorRef? _sensorRef;

    public PipelineActor(
        IOpenMeteoClient client,
        TimeProvider timeProvider,
        IBudgetGate<WeightedTarget> budgetGate,
        ConsensusSnapshotFactory consensusFactory,
        AlertAnalyzer alerts,
        DerivedAnalyzer derived,
        TrendAnalyzer trends,
        IndexAnalyzer indices)
    {
        _client = client;
        _timeProvider = timeProvider;
        _budgetGate = budgetGate;
        _consensusFactory = consensusFactory;
        _alerts = alerts;
        _derived = derived;
        _trends = trends;
        _indices = indices;
        _scheduler = Context.GetActor<ISchedulerActor>();

        Ready();
    }

    protected override void PreStart()
    {
        _log = Context.GetLogger();
        _mat = Context.Materializer();
        MaterializePipeline(_scheduler);
        _log.Info("Pipeline graph materialized - ready to accept polls");
    }

    private void Ready()
    {
        Receive<WeightedTarget>(target => _queue?.OfferAsync(target));

        Receive<SensorSnapshot>(snapshot => _sensorRef?.Tell(snapshot));

        Receive<StopStreams>(_ =>
        {
            _killSwitch?.Shutdown();
            _queue?.Complete();
            _sensorRef?.Tell(new Status.Success("shutdown"));
            Sender.Tell(new StreamsStopped());
        });

        Receive<RequestPipelineOutput>(msg =>
        {
            _pipelineOutputSource!
                .RunWith(StreamRefs.SourceRef<PipelineEvent>(), _mat!)
                .PipeTo(Sender, Self,
                    sr => new PipelineOutputResponse(msg.RequestId, sr),
                    ex =>
                    {
                        _log.Error(ex, "Failed to create pipeline output SourceRef");
                        return new PipelineOutputFailed(msg.RequestId, ex);
                    });
        });
    }

    private void MaterializePipeline(IActorRef schedulerActor)
    {
        var (queue, queueSource) = Source.Queue<WeightedTarget>(16, OverflowStrategy.Backpressure)
            .PreMaterialize(_mat);

        var (pipelineOutputSource, pipelineOutputSink) = BroadcastHub.Sink<PipelineEvent>(bufferSize: 2)
            .PreMaterialize(_mat);

        var emptySensors = new SensorSnapshot("", new Dictionary<SensorKind, AggregatedReading>());
        var (sensorRef, sensorSourceRaw) = Source.ActorRef<SensorSnapshot>(10, OverflowStrategy.DropHead)
            .PreMaterialize(_mat);
        var sensorSource = Source.Single(emptySensors)
            .Concat(sensorSourceRaw)
            .Via(new ReuseLatest<SensorSnapshot>());

        var consensusFactory = _consensusFactory;
        var alerts = _alerts;
        var derived = _derived;
        var trends = _trends;
        var indices = _indices;
        var timeProvider = _timeProvider;

        var killSwitch = queueSource
            .ViaMaterialized(KillSwitches.Single<WeightedTarget>(), Keep.Right)
            .Via(new BudgetThrottleStage<WeightedTarget>(_budgetGate))
            .Log("pipeline-fetch-in", t => $"{t.Location.Name}/{t.Model.Id}", _log)
            .SelectAsyncUnordered(2, t =>
                _client.FetchAsync(t.Location, t.Model, t.Cycle, CancellationToken.None))
            .Log("pipeline-fetch-out", o => o switch
            {
                PollOutcome.Success s => $"OK {s.Forecast.Location}/{s.Forecast.Model.Id}",
                PollOutcome.Failure f => $"FAIL {f.Location}/{f.Model.Id} {f.Reason}",
                _ => "?",
            }, _log)
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .Buffer(32, OverflowStrategy.Backpressure)
            .Select(outcome =>
            {
                switch (outcome)
                {
                    case PollOutcome.Success s:
                        schedulerActor.Tell(new HashResult(s.Forecast.Location,
                            s.Forecast.Model.Id, ForecastDataHash.Compute(s.Forecast, _timeProvider)));
                        break;
                    case PollOutcome.Failure f:
                        schedulerActor.Tell(new FetchFailed(f.Location, f.Model.Id, f.Reason, f.Detail));
                        break;
                }
                return outcome;
            })
            .Collect(o => o is PollOutcome.Success, o => ((PollOutcome.Success)o).Forecast)
            .Via(Flow.FromGraph(GraphDsl.Create(builder =>
            {
                var zip = builder.Add(ZipWith.Apply<Forecast, SensorSnapshot, (Forecast, SensorSnapshot)>(
                    (forecast, sensors) => (forecast, sensors)));
                var sensor = builder.Add(sensorSource);
                builder.From(sensor).To(zip.In1);
                return new FlowShape<Forecast, (Forecast, SensorSnapshot)>(zip.In0, zip.Out);
            })))
            .StatefulSelectMany<(Forecast forecast, SensorSnapshot sensors), PipelineEvent, UniqueKillSwitch>(() =>
            {
                var snapshot = ModelSnapshot.Empty;
                ConsensusSnapshot? previous = null;

                return input =>
                {
                    var now = timeProvider.GetUtcNow();
                    snapshot = snapshot.Update(input.forecast);
                    var consensus = consensusFactory.Create(snapshot, input.forecast.Location);

                    var events = new List<PipelineEvent>
                    {
                        new PipelineEvent.ForecastReceived(input.forecast.Location, input.forecast.Model, input.forecast),
                        new PipelineEvent.ConsensusComputed(consensus, now)
                    };

                    if (alerts.Compute(consensus) is { } a)
                    {
                        events.Add(new PipelineEvent.AlertComputed(a, now));
                    }

                    if (derived.Compute(consensus) is { } d)
                    {
                        events.Add(new PipelineEvent.DerivedComputed(d, now));
                    }

                    if (trends.Compute(consensus, previous) is { } tr)
                    {
                        events.Add(new PipelineEvent.TrendComputed(tr, now));
                    }

                    var sensors = input.sensors.Readings.Count > 0 ? input.sensors : null;
                    if (indices.Compute(consensus, sensors) is { } ix)
                    {
                        events.Add(new PipelineEvent.IndexComputed(ix, now));
                    }

                    previous = consensus;
                    return events;
                };
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .ToMaterialized(pipelineOutputSink, Keep.Left)
            .Run(_mat);

        _queue = queue;
        _killSwitch = killSwitch;
        _pipelineOutputSource = pipelineOutputSource;
        _sensorRef = sensorRef;
    }
}
