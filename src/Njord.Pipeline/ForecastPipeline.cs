using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Consensus;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Derived;
using Njord.Indices;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;
using Njord.Messages.Sensors;
using Njord.Pipeline.Configuration;
using Njord.Weather;
using Servus.Akka;
using TrendAnalyzer = Njord.Trends.TrendAnalyzer;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline;

// Owns the long-lived hubs: producers attach to the MergeHub input via SinkRef, consumers to the
// BroadcastHub output via SourceRef. Both live as long as this incarnation; producers and consumers
// coming and going never rebuild them, so the analysis state in the stage closure survives them.
public sealed class ForecastPipeline : ReceiveActor
{
    private const int PerProducerBufferSize = 16;

    // Unique across incarnations: a restarted pipeline keeps its ActorRef, and the end of the previous
    // incarnation's graph must not fail the new one.
    private static long LastGraphId;

    private sealed record AnalysisGraphEnded(long GraphId, Exception? Cause) : IDeadLetterSuppression;

    private readonly TimeProvider _timeProvider;
    private readonly ConsensusBuilder _consensusBuilder;
    private readonly bool _consensusEnabled;
    private readonly AlertAnalyzer _alerts;
    private readonly DerivedAnalyzer _derived;
    private readonly TrendAnalyzer _trends;
    private readonly IndexAnalyzer _indices;
    private readonly AnalysisSettings _startupSettings;

    private IMaterializer? _mat;
    private long _graphId;
    private Sink<WeatherForecast, NotUsed>? _mergeSink;
    private Source<PipelineEvent, NotUsed>? _pipelineOutputSource;
    private IActorRef? _sensorRef;
    private IActorRef? _settingsRef;

    private readonly ILoggingAdapter _log = Context.GetLogger();

    public ForecastPipeline(
        TimeProvider timeProvider,
        ConsensusBuilder consensusBuilder,
        IOptions<ConsensusOptions> consensusOptions,
        AlertAnalyzer alerts,
        DerivedAnalyzer derived,
        TrendAnalyzer trends,
        IndexAnalyzer indices,
        AnalysisSettings startupSettings)
    {
        _timeProvider = timeProvider;
        _consensusBuilder = consensusBuilder;
        _consensusEnabled = consensusOptions.Value.Enabled;
        _alerts = alerts;
        _derived = derived;
        _trends = trends;
        _indices = indices;
        _startupSettings = startupSettings;

        Receive<RequestForecastSink>(OnRequestForecastSink);
        Receive<RequestPipelineOutput>(OnRequestPipelineOutput);
        Receive<AnalysisSettingsChanged>(msg => _settingsRef!.Tell(msg.Settings));
        Receive<AnalysisSettingsResult>(msg => _settingsRef!.Tell(msg.Settings));
        Receive<LocationSensorReadings>(msg => _sensorRef!.Tell(msg));
        Receive<AnalysisGraphEnded>(OnAnalysisGraphEnded);
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();
        MaterializeAnalysisPipeline();
        _log.Info("Forecast pipeline materialized - waiting for producers and consumers");
        RequestCurrentInputs();
    }

    // A restarted pipeline would otherwise analyze with the startup settings and an empty sensor map until the next
    // settings change or sensor push. Neither answer is awaited: until it arrives, the seeds of the side channels apply.
    private void RequestCurrentInputs()
    {
        if (Context.TryGetActor<IConfigManager>(out var configManager))
        {
            configManager.Tell(new QueryAnalysisSettings());
        }
        else
        {
            _log.Debug("No config manager registered - analyzing with the startup settings");
        }

        if (Context.TryGetActor<ISensorHub>(out var sensorHub))
        {
            sensorHub.Tell(new RequestSensorReadings());
        }
        else
        {
            _log.Debug("No sensor hub registered - analyzing without live sensor readings until one pushes");
        }
    }

    private void OnRequestForecastSink(RequestForecastSink msg)
    {
        // A producer's failure must never end the MergeHub. Akka.NET 1.5.73 already drops a failed producer
        // (logging an error) although the MergeHub docs say it fails the hub; ending each producer's stream as a
        // completion keeps the hub safe either way and logs the loss as a warning.
        var log = _log;
        StreamRefs.SinkRef<WeatherForecast>()
            .Via(Flow.Create<WeatherForecast>().RecoverWithRetries(ex =>
            {
                log.Warning(ex, "Forecast producer stream failed - detaching it from the pipeline input");
                return Source.Empty<WeatherForecast>();
            }, 1))
            .To(_mergeSink!)
            .Run(_mat!)
            .PipeTo(Sender, Self,
                sinkRef => new RequestForecastSinkCompleted(msg.RequestId, sinkRef),
                ex =>
                {
                    _log.Error(ex, "Failed to create forecast input SinkRef");
                    return new RequestForecastSinkFailed(msg.RequestId, ex);
                });
    }

    private void OnRequestPipelineOutput(RequestPipelineOutput msg)
    {
        _pipelineOutputSource!
            .RunWith(StreamRefs.SourceRef<PipelineEvent>(), _mat!)
            .PipeTo(Sender, Self,
                sourceRef => new RequestPipelineOutputCompleted(msg.RequestId, sourceRef),
                ex =>
                {
                    _log.Error(ex, "Failed to create pipeline output SourceRef");
                    return new RequestPipelineOutputFailed(msg.RequestId, ex);
                });
    }

    // The hubs are gone with the graph: fail, so the supervisor restarts the pipeline with fresh hubs and every
    // producer and consumer re-attaches.
    private void OnAnalysisGraphEnded(AnalysisGraphEnded msg)
    {
        if (msg.GraphId != _graphId)
        {
            return;
        }

        throw new InvalidOperationException("Forecast analysis graph ended unexpectedly", msg.Cause);
    }

    private void MaterializeAnalysisPipeline()
    {
        var graphId = _graphId = Interlocked.Increment(ref LastGraphId);
        var (mergeSink, forecastSource) = MergeHub.Source<WeatherForecast>(PerProducerBufferSize).PreMaterialize(_mat);

        var (pipelineOutputSource, pipelineOutputSink) = BroadcastHub.Sink<PipelineEvent>(bufferSize: 2)
            .PreMaterialize(_mat);

        // The latest settings and sensor readings are zipped onto every forecast, so a change applies from the
        // next forecast on and never in the middle of one computation.
        var (settingsRef, settingsSourceRaw) = Source.ActorRef<AnalysisSettings>(10, OverflowStrategy.DropHead)
            .PreMaterialize(_mat);
        var settingsSource = Source.Single(_startupSettings)
            .Concat(settingsSourceRaw)
            .RepeatPrevious();

        var (sensorRef, sensorSourceRaw) = Source.ActorRef<LocationSensorReadings>(10, OverflowStrategy.DropHead)
            .PreMaterialize(_mat);
        var sensorSource = Source.Single(LocationSensorReadings.Empty)
            .Concat(sensorSourceRaw)
            .RepeatPrevious();

        var consensusBuilder = _consensusBuilder;
        var consensusEnabled = _consensusEnabled;
        var alerts = _alerts;
        var derived = _derived;
        var trends = _trends;
        var indices = _indices;
        var timeProvider = _timeProvider;
        var log = _log;

        forecastSource
            .Via(Flow.FromGraph(GraphDsl.Create(builder =>
            {
                var zip = builder.Add(ZipWith.Apply<WeatherForecast, LocationSensorReadings, AnalysisSettings,
                    (WeatherForecast, LocationSensorReadings, AnalysisSettings)>(
                    (forecast, sensors, settings) => (forecast, sensors, settings)));
                builder.From(builder.Add(sensorSource)).To(zip.In1);
                builder.From(builder.Add(settingsSource)).To(zip.In2);
                return new FlowShape<WeatherForecast, (WeatherForecast, LocationSensorReadings, AnalysisSettings)>(zip.In0, zip.Out);
            })))
            .StatefulSelectMany<(WeatherForecast forecast, LocationSensorReadings sensors, AnalysisSettings settings), PipelineEvent, NotUsed>(() =>
            {
                var forecasts = ModelForecasts.Empty;
                LocationConsensus? previous = null;

                return input =>
                {
                    var now = timeProvider.GetUtcNow();
                    var location = new LocationId(input.forecast.Location);
                    forecasts = forecasts.Update(input.forecast);
                    var settings = input.settings;
                    // The forecasts feed every analysis, so it is built even when its own output is switched off.
                    var consensus = consensusBuilder.Create(forecasts, input.forecast.Location, settings.Consensus);

                    var events = new List<PipelineEvent>
                    {
                        new PipelineEvent.ForecastReceived(location, input.forecast.Model, input.forecast),
                    };

                    if (consensusEnabled)
                        events.Add(new PipelineEvent.ConsensusComputed(consensus, now));

                    if (alerts.Compute(consensus, settings.Alerts) is { } a)
                        events.Add(new PipelineEvent.AlertComputed(a, now));

                    if (derived.Compute(consensus, settings.Derived) is { } d)
                        events.Add(new PipelineEvent.DerivedComputed(d, now));

                    if (trends.Compute(consensus, previous, settings.Trends) is { } tr)
                        events.Add(new PipelineEvent.TrendComputed(tr, now));

                    if (indices.Compute(consensus, input.sensors.For(location), settings.Indices) is { } ix)
                        events.Add(new PipelineEvent.IndexComputed(ix, now));

                    previous = consensus;
                    log.Debug("Analyzed {0}/{1}: {2} events, live sensors {3}",
                        location, input.forecast.Model.Id, events.Count, input.sensors.For(location) is null ? "absent" : "present");
                    return events;
                };
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .WatchTermination(Keep.Right)
            .To(pipelineOutputSink)
            .Run(_mat)
            .PipeTo(Self,
                success: () => new AnalysisGraphEnded(graphId, null),
                failure: ex => new AnalysisGraphEnded(graphId, ex));

        _mergeSink = mergeSink;
        _pipelineOutputSource = pipelineOutputSource;
        _settingsRef = settingsRef;
        _sensorRef = sensorRef;
    }
}
