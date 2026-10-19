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
using Njord.Forecast;
using Njord.Indices;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Pipeline.Configuration;
using Servus.Akka;
using TrendAnalyzer = Njord.Trends.TrendAnalyzer;
using WeatherForecast = Njord.Forecast.Forecast;

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
    private readonly AnalysisSettings _settings;

    private IMaterializer? _mat;
    private long _graphId;
    private Sink<WeatherForecast, NotUsed>? _mergeSink;
    private Source<PipelineEvent, NotUsed>? _pipelineOutputSource;
    private Sink<PipelineEvent, NotUsed>? _pipelineOutputSink;
    private Source<WeatherForecast, NotUsed>? _forecastSource;

    private readonly ILoggingAdapter _log = Context.GetLogger();

    public ForecastPipeline(
        TimeProvider timeProvider,
        ConsensusBuilder consensusBuilder,
        IOptions<ConsensusOptions> consensusOptions,
        AlertAnalyzer alerts,
        DerivedAnalyzer derived,
        TrendAnalyzer trends,
        IndexAnalyzer indices,
        AnalysisSettings settings)
    {
        _timeProvider = timeProvider;
        _consensusBuilder = consensusBuilder;
        _consensusEnabled = consensusOptions.Value.Enabled;
        _alerts = alerts;
        _derived = derived;
        _trends = trends;
        _indices = indices;
        _settings = settings;

        Receive<RequestForecastSink>(OnRequestForecastSink);
        Receive<RequestPipelineOutput>(OnRequestPipelineOutput);
        Receive<AnalysisGraphEnded>(OnAnalysisGraphEnded);
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();
        MaterializeHubs();
        MaterializeAnalysisGraph();
        _log.Info("Forecast pipeline materialized - waiting for producers and consumers");
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

    private void OnAnalysisGraphEnded(AnalysisGraphEnded msg)
    {
        if (msg.GraphId != _graphId)
        {
            return;
        }

        _log.Warning(msg.Cause, "Analysis graph ended — re-materializing (hubs and consumers unaffected)");
        MaterializeAnalysisGraph();
    }

    private void MaterializeHubs()
    {
        var (mergeSink, forecastSource) = MergeHub.Source<WeatherForecast>(PerProducerBufferSize).PreMaterialize(_mat);

        var (pipelineOutputSource, pipelineOutputSink) = BroadcastHub.Sink<PipelineEvent>(bufferSize: 2)
            .PreMaterialize(_mat);

        _mergeSink = mergeSink;
        _pipelineOutputSource = pipelineOutputSource;
        _pipelineOutputSink = pipelineOutputSink;
        _forecastSource = forecastSource;
    }

    private void MaterializeAnalysisGraph()
    {
        var graphId = _graphId = Interlocked.Increment(ref LastGraphId);
        var forecastSource = _forecastSource!;
        var pipelineOutputSink = _pipelineOutputSink!;
        var settings = _settings;

        var consensusBuilder = _consensusBuilder;
        var consensusEnabled = _consensusEnabled;
        var alerts = _alerts;
        var derived = _derived;
        var trends = _trends;
        var indices = _indices;
        var timeProvider = _timeProvider;
        var log = _log;

        forecastSource
            .StatefulSelectMany<WeatherForecast, PipelineEvent, NotUsed>(() =>
            {
                var forecasts = ModelForecasts.Empty;
                LocationConsensus? previous = null;

                return forecast =>
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var now = timeProvider.GetUtcNow();
                    var location = new LocationId(forecast.Location);
                    forecasts = forecasts.Update(forecast);
                    var consensus = consensusBuilder.Create(forecasts, forecast.Location, settings.Consensus);

                    var events = new List<PipelineEvent>(6)
                    {
                        new PipelineEvent.ForecastReceived(location, forecast.Model, forecast),
                    };

                    if (consensusEnabled)
                    {
                        events.Add(new PipelineEvent.ConsensusComputed(consensus, now));
                    }

                    if (alerts.Compute(consensus, settings.Alerts) is { } a)
                    {
                        events.Add(new PipelineEvent.AlertComputed(a, now));
                    }

                    if (derived.Compute(consensus, settings.Derived) is { } d)
                    {
                        events.Add(new PipelineEvent.DerivedComputed(d, now));
                    }

                    if (trends.Compute(consensus, previous, settings.Trends) is { } tr)
                    {
                        events.Add(new PipelineEvent.TrendComputed(tr, now));
                    }

                    if (indices.Compute(consensus, settings.Indices) is { } ix)
                    {
                        events.Add(new PipelineEvent.IndexComputed(ix, now));
                    }

                    previous = consensus;
                    sw.Stop();
                    Telemetry.EnrichmentDuration.Record(sw.Elapsed.TotalSeconds);
                    foreach (var evt in events)
                    {
                        Telemetry.PipelineEvents.Add(1, new KeyValuePair<string, object?>("event_type", evt.GetType().Name));
                    }

                    log.Debug("Analyzed {0}/{1}: {2} events",
                        location, forecast.Model.Id, events.Count);
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
    }
}
