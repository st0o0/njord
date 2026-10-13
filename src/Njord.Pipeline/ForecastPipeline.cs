using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Alerts;
using Njord.Compute.Analysis;
using Njord.Compute.Settings;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Egress;
using Njord.Derived;
using Njord.Indices;
using Njord.Messages.Forecast;
using Njord.Sensors;
using Njord.Weather;
using Servus.Akka;
using Servus.Resilience;
using TrendAnalyzer = Njord.Trends.TrendAnalyzer;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline;

public sealed class ForecastPipeline : ReceiveActor, IWithStash
{
    private static readonly BackoffPolicy GatewayRetryPolicy =
        Backoff.Create(TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromSeconds(30));

    private sealed record GatewayResolved(IActorRef Gateway);
    private sealed record GatewayResolveFailed(Exception Cause);
    private sealed record RetryGatewayResolve;

    private readonly TimeProvider _timeProvider;
    private readonly ConsensusSnapshotFactory _consensusFactory;
    private readonly AlertAnalyzer _alerts;
    private readonly DerivedAnalyzer _derived;
    private readonly TrendAnalyzer _trends;
    private readonly IndexAnalyzer _indices;

    private Source<PipelineEvent, NotUsed>? _pipelineOutputSource;
    private IMaterializer? _mat;
    private IActorRef? _sensorRef;
    private IActorRef? _settingsRef;
    private AnalysisSettings _settings;
    private IActorRef? _gateway;
    private IActorRef? _lastTerminatedGateway;
    private int _gatewayRetryCount;

    public IStash Stash { get; set; } = null!;

    private readonly ILoggingAdapter _log = Context.GetLogger();

    public ForecastPipeline(
        TimeProvider timeProvider,
        ConsensusSnapshotFactory consensusFactory,
        AlertAnalyzer alerts,
        DerivedAnalyzer derived,
        TrendAnalyzer trends,
        IndexAnalyzer indices,
        AnalysisSettings startupSettings)
    {
        _settings = startupSettings;
        _timeProvider = timeProvider;
        _consensusFactory = consensusFactory;
        _alerts = alerts;
        _derived = derived;
        _trends = trends;
        _indices = indices;

        ReceiveAny(_ => Stash.Stash());
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();

        Context.GetActorAsync<IForecastGateway>()
            .PipeTo(Self, success: r => new GatewayResolved(r), failure: ex => new GatewayResolveFailed(ex));

        WaitingForGateway();
    }

    private void WaitingForGateway()
    {
        Become(() =>
        {
            Receive<GatewayResolved>(msg =>
            {
                if (Equals(msg.Gateway, _lastTerminatedGateway))
                {
                    var delay = GatewayRetryPolicy.DelayWithJitter(_gatewayRetryCount);
                    _gatewayRetryCount++;
                    Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, new RetryGatewayResolve(), Self);
                    return;
                }

                _lastTerminatedGateway = null;
                _gateway = msg.Gateway;
                Context.Watch(_gateway);
                _gatewayRetryCount = 0;

                var requestId = Guid.NewGuid();
                _gateway.Tell(new RequestForecastStream(requestId));
            });

            Receive<RequestForecastStreamCompleted>(msg =>
            {
                _log.Info("Received forecast SourceRef from gateway — materializing analysis pipeline");
                MaterializeAnalysisPipeline(msg.SourceRef.Source);
                EnterReady();
                Stash.UnstashAll();
            });

            Receive<RequestForecastStreamFailed>(msg =>
            {
                _log.Warning(msg.Cause, "Failed to get forecast stream from gateway — retrying");
                var delay = GatewayRetryPolicy.DelayWithJitter(_gatewayRetryCount);
                _gatewayRetryCount++;
                Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, new RetryGatewayResolve(), Self);
            });

            Receive<RetryGatewayResolve>(_ =>
            {
                Context.GetActorAsync<IForecastGateway>()
                    .PipeTo(Self, success: r => new GatewayResolved(r), failure: ex => new GatewayResolveFailed(ex));
            });

            Receive<GatewayResolveFailed>(msg =>
            {
                _log.Warning(msg.Cause, "Failed to resolve gateway — retrying");
                var delay = GatewayRetryPolicy.DelayWithJitter(_gatewayRetryCount);
                _gatewayRetryCount++;
                Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, new RetryGatewayResolve(), Self);
            });

            Receive<AnalysisSettingsChanged>(msg => _settings = msg.Settings);

            Receive<Terminated>(OnTerminated);

            ReceiveAny(_ => Stash.Stash());
        });
    }

    private void EnterReady()
    {
        Become(() =>
        {
            Receive<SensorSnapshot>(snapshot => _sensorRef?.Tell(snapshot));

            Receive<AnalysisSettingsChanged>(msg =>
            {
                _settings = msg.Settings;
                _settingsRef?.Tell(msg.Settings);
            });

            Receive<RequestPipelineOutput>(msg =>
            {
                _pipelineOutputSource!
                    .RunWith(StreamRefs.SourceRef<PipelineEvent>(), _mat!)
                    .PipeTo(Sender, Self,
                        sr => new RequestPipelineOutputCompleted(msg.RequestId, sr),
                        ex =>
                        {
                            _log.Error(ex, "Failed to create pipeline output SourceRef");
                            return new RequestPipelineOutputFailed(msg.RequestId, ex);
                        });
            });

            Receive<Terminated>(OnTerminated);
        });
    }

    private void OnTerminated(Terminated msg)
    {
        if (!Equals(msg.ActorRef, _gateway))
            return;

        _log.Warning("Gateway terminated — re-resolving");
        _lastTerminatedGateway = msg.ActorRef;
        _gateway = null;
        _gatewayRetryCount = 0;

        Context.GetActorAsync<IForecastGateway>()
            .PipeTo(Self, success: r => new GatewayResolved(r), failure: ex => new GatewayResolveFailed(ex));

        WaitingForGateway();
    }

    private void MaterializeAnalysisPipeline(Source<WeatherForecast, NotUsed> forecastSource)
    {
        var (pipelineOutputSource, pipelineOutputSink) = BroadcastHub.Sink<PipelineEvent>(bufferSize: 2)
            .PreMaterialize(_mat);

        var emptySensors = new SensorSnapshot("", new Dictionary<SensorKind, AggregatedReading>());
        var (sensorRef, sensorSourceRaw) = Source.ActorRef<SensorSnapshot>(10, OverflowStrategy.DropHead)
            .PreMaterialize(_mat);
        var sensorSource = Source.Single(emptySensors)
            .Concat(sensorSourceRaw)
            .RepeatPrevious();

        // Same side-channel shape as the sensors: the latest settings are zipped onto every forecast, so a
        // change applies from the next forecast on and never in the middle of one computation.
        var (settingsRef, settingsSourceRaw) = Source.ActorRef<AnalysisSettings>(10, OverflowStrategy.DropHead)
            .PreMaterialize(_mat);
        var settingsSource = Source.Single(_settings)
            .Concat(settingsSourceRaw)
            .RepeatPrevious();

        var consensusFactory = _consensusFactory;
        var alerts = _alerts;
        var derived = _derived;
        var trends = _trends;
        var indices = _indices;
        var timeProvider = _timeProvider;

        forecastSource
            .Via(Flow.FromGraph(GraphDsl.Create(builder =>
            {
                var zip = builder.Add(ZipWith.Apply<WeatherForecast, SensorSnapshot, AnalysisSettings, (WeatherForecast, SensorSnapshot, AnalysisSettings)>(
                    (forecast, sensors, settings) => (forecast, sensors, settings)));
                var sensor = builder.Add(sensorSource);
                var settings = builder.Add(settingsSource);
                builder.From(sensor).To(zip.In1);
                builder.From(settings).To(zip.In2);
                return new FlowShape<WeatherForecast, (WeatherForecast, SensorSnapshot, AnalysisSettings)>(zip.In0, zip.Out);
            })))
            .StatefulSelectMany<(WeatherForecast forecast, SensorSnapshot sensors, AnalysisSettings settings), PipelineEvent, NotUsed>(() =>
            {
                var snapshot = ModelSnapshot.Empty;
                ConsensusSnapshot? previous = null;

                return input =>
                {
                    var now = timeProvider.GetUtcNow();
                    snapshot = snapshot.Update(input.forecast);
                    var settings = input.settings;
                    var consensus = consensusFactory.Create(snapshot, input.forecast.Location, settings.Consensus);

                    var events = new List<PipelineEvent>
                    {
                        new PipelineEvent.ForecastReceived(input.forecast.Location, input.forecast.Model, input.forecast),
                        new PipelineEvent.ConsensusComputed(consensus, now)
                    };

                    if (alerts.Compute(consensus, settings.Alerts) is { } a)
                        events.Add(new PipelineEvent.AlertComputed(a, now));

                    if (derived.Compute(consensus, settings.Derived) is { } d)
                        events.Add(new PipelineEvent.DerivedComputed(d, now));

                    if (trends.Compute(consensus, previous, settings.Trends) is { } tr)
                        events.Add(new PipelineEvent.TrendComputed(tr, now));

                    var sensors = input.sensors.Readings.Count > 0 ? input.sensors : null;
                    if (indices.Compute(consensus, sensors, settings.Indices) is { } ix)
                        events.Add(new PipelineEvent.IndexComputed(ix, now));

                    previous = consensus;
                    return events;
                };
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .To(pipelineOutputSink)
            .Run(_mat);

        _pipelineOutputSource = pipelineOutputSource;
        _sensorRef = sensorRef;
        _settingsRef = settingsRef;
    }
}
