using System.Collections.Immutable;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core.Actors;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Grpc;

public sealed class GrpcConsumer : StreamConsumerActor
{
    private const int StreamClientBufferSize = 256;

    private readonly ILoggingAdapter _log = Context.GetLogger();

    private IActorRef _hubInput = ActorRefs.Nobody;
    private Source<PipelineEvent, NotUsed> _clientHubSource = Source.Empty<PipelineEvent>();

    private ImmutableDictionary<LocationId, LatestResults> _state = ImmutableDictionary<LocationId, LatestResults>.Empty;

    private sealed record LatestResults
    {
        public IReadOnlyDictionary<WeatherModel, WeatherForecast> Forecasts { get; init; } =
            new Dictionary<WeatherModel, WeatherForecast>();

        public LatestAnalysisResult? Analysis { get; init; }
    }

    private sealed record StreamCompleted;

    public GrpcConsumer()
    {
        Receive<QueryForecast>(HandleQueryForecast);
        Receive<QueryLatestAnalysis>(HandleQueryLatestAnalysis);
        Receive<RequestGrpcStream>(HandleStreamRequest);
        Receive<PipelineEvent>(OnPipelineEvent);
        Receive<StreamCompleted>(_ => { });
    }

    protected override void PreStart()
    {
        base.PreStart();
        MaterializeClientHub();
    }

    private void MaterializeClientHub()
    {
        var (hubInput, hubSource) = Source.ActorRef<PipelineEvent>(256, OverflowStrategy.DropHead)
            .ToMaterialized(BroadcastHub.Sink<PipelineEvent>(bufferSize: 2), Keep.Both)
            .Run(Mat);

        hubSource.RunWith(Sink.Ignore<PipelineEvent>(), Mat);

        _hubInput = hubInput;
        _clientHubSource = hubSource;
    }

    protected override Task MaterializeGraph(Source<PipelineEvent, NotUsed> events) =>
        events
            .WatchTermination(Keep.Right)
            .To(Sink.ActorRef<PipelineEvent>(Self, new StreamCompleted(), ex => new StreamCompleted()))
            .Run(Mat);

    private void OnPipelineEvent(PipelineEvent evt)
    {
        UpdateState(evt);
        _hubInput.Tell(evt);
    }

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

    private void HandleStreamRequest(RequestGrpcStream msg)
    {
        _clientHubSource
            .Buffer(StreamClientBufferSize, OverflowStrategy.DropHead)
            .RunWith(StreamRefs.SourceRef<PipelineEvent>(), Mat)
            .PipeTo(Sender, Self,
                sr => new RequestGrpcStreamCompleted(msg.RequestId, sr),
                ex =>
                {
                    _log.Error(ex, "Failed to create gRPC stream SourceRef");
                    return new RequestGrpcStreamFailed(msg.RequestId, ex);
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
