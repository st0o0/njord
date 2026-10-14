using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;
using Njord.Core.Actors;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Weather;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline;

// In-memory projection of the pipeline output for the API: the latest forecast per location and model,
// the latest event of each analysis kind per location, and a secondary hub for streaming clients.
public sealed class ForecastReadModel : StreamConsumerActor
{
    // Per streaming client; a client that falls behind loses its oldest events instead of holding up the hub.
    private const int StreamClientBufferSize = 256;

    private readonly ILoggingAdapter _log = Context.GetLogger();

    private Sink<PipelineEvent, NotUsed> _clientHubSink = null!;
    private Source<PipelineEvent, NotUsed> _clientHubSource = null!;

    // Keyed by LocationId, whose equality ignores case, so a query matches whatever spelling it uses.
    // Written only by the materialized stream and read by the actor: an immutable map swapped as a whole.
    private volatile ImmutableDictionary<LocationId, LatestResults> _state = ImmutableDictionary<LocationId, LatestResults>.Empty;

    private sealed record LatestResults
    {
        public IReadOnlyDictionary<WeatherModel, WeatherForecast> Forecasts { get; init; } =
            new Dictionary<WeatherModel, WeatherForecast>();

        public LatestAnalysisResult? Analysis { get; init; }
    }

    public ForecastReadModel()
    {
        Receive<QueryForecast>(HandleQueryForecast);
        Receive<QueryLatestAnalysis>(HandleQueryLatestAnalysis);
        Receive<RequestReadModelStream>(HandleStreamRequest);
    }

    protected override void PreStart()
    {
        base.PreStart();
        MaterializeClientHub();
    }

    // Once per actor incarnation, before the first subscription: streaming clients can attach at any time, and every
    // pipeline subscription feeds the same MergeHub, so their streams survive a re-attach.
    private void MaterializeClientHub()
    {
        var (mergeSink, mergeSource) = MergeHub.Source<PipelineEvent>(perProducerBufferSize: 16).PreMaterialize(Mat);
        var hubSource = mergeSource.RunWith(BroadcastHub.Sink<PipelineEvent>(bufferSize: 2), Mat);

        // A BroadcastHub without a consumer backpressures instead of dropping: this permanent drain keeps the
        // read model (and with it the pipeline and MQTT) consuming while no streaming client is attached.
        hubSource.RunWith(Sink.Ignore<PipelineEvent>(), Mat);

        _clientHubSink = mergeSink;
        _clientHubSource = hubSource;
    }

    protected override Task MaterializeGraph(Source<PipelineEvent, NotUsed> events) =>
        events
            .Select(evt =>
            {
                UpdateState(evt);
                return evt;
            })
            .WatchTermination(Keep.Right)
            // The termination above already reports a failed subscription to the base class; it must not fail the
            // MergeHub, which outlives the subscription.
            .Recover(_ => Option<PipelineEvent>.None)
            .To(_clientHubSink)
            .Run(Mat);

    private void HandleQueryForecast(QueryForecast msg)
    {
        if (_state.TryGetValue(msg.Location, out var latest))
        {
            foreach (var (model, forecast) in latest.Forecasts)
            {
                if (string.Equals(model.Id, msg.ModelId, StringComparison.OrdinalIgnoreCase))
                {
                    Sender.Tell(new ForecastResult(forecast));
                    return;
                }
            }
        }

        Sender.Tell(new ForecastNotAvailable($"{msg.Location.Name}|{msg.ModelId}"));
    }

    private void HandleQueryLatestAnalysis(QueryLatestAnalysis msg)
    {
        Sender.Tell(_state.TryGetValue(msg.Location, out var latest) && latest.Analysis is { } analysis
            ? analysis
            : new LatestAnalysisNotAvailable(msg.Location));
    }

    private void HandleStreamRequest(RequestReadModelStream msg)
    {
        _clientHubSource
            .Buffer(StreamClientBufferSize, OverflowStrategy.DropHead)
            .RunWith(StreamRefs.SourceRef<PipelineEvent>(), Mat)
            .PipeTo(Sender, Self,
                sr => new RequestReadModelStreamCompleted(msg.RequestId, sr),
                ex =>
                {
                    _log.Error(ex, "Failed to create read model stream SourceRef");
                    return new RequestReadModelStreamFailed(msg.RequestId, ex);
                });
    }

    private void UpdateState(PipelineEvent evt)
    {
        switch (evt)
        {
            case PipelineEvent.ForecastReceived forecast:
                var forecastState = _state.GetValueOrDefault(forecast.Location, new LatestResults());
                var forecasts = new Dictionary<WeatherModel, WeatherForecast>(forecastState.Forecasts)
                {
                    [forecast.Model] = forecast.Forecast
                };
                _state = _state.SetItem(forecast.Location, forecastState with { Forecasts = forecasts });
                break;
            case IEnrichmentEvent enrichment:
                var location = enrichment.Location;
                var analysisState = _state.GetValueOrDefault(location, new LatestResults());
                var analysis = analysisState.Analysis ?? new LatestAnalysisResult(location, null, null, null, null, null);
                _state = _state.SetItem(location, analysisState with { Analysis = WithLatest(analysis, evt) });
                break;
        }
    }

    private static LatestAnalysisResult WithLatest(LatestAnalysisResult analysis, PipelineEvent evt) => evt switch
    {
        PipelineEvent.ConsensusComputed consensus => analysis with { Consensus = consensus },
        PipelineEvent.AlertComputed alerts => analysis with { Alerts = alerts },
        PipelineEvent.DerivedComputed derived => analysis with { Derived = derived },
        PipelineEvent.TrendComputed trends => analysis with { Trends = trends },
        PipelineEvent.IndexComputed indices => analysis with { Indices = indices },
        _ => analysis,
    };
}
