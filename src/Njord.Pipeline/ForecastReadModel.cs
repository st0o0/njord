using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core.Actors;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Weather;
using Servus.Akka;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline;

// In-memory projection of the pipeline output for the API: the latest forecast per location and model,
// the latest analysis event per location, and a secondary hub for streaming clients.
public sealed class ForecastReadModel : StreamConsumerActor
{
    // Per streaming client; a client that falls behind loses its oldest events instead of holding up the hub.
    private const int StreamClientBufferSize = 256;

    private readonly ILoggingAdapter _log = Context.GetLogger();

    private IActorRef? _pipelineRef;
    private ISourceRef<PipelineEvent>? _pipelineSourceRef;
    private long _pipelineSourceRequestId;

    private Source<PipelineEvent, NotUsed>? _secondaryHubSource;

    // Keyed by the location name as the pipeline reports it (ordinal), as the read model always was.
    private readonly Dictionary<string, LatestResults> _state = new();

    private sealed record PipelineResolved(IActorRef Ref);
    private sealed record PipelineResolveFailed(Exception Cause);

    private sealed record LatestResults
    {
        public IReadOnlyDictionary<WeatherModel, WeatherForecast> Forecasts { get; init; } =
            new Dictionary<WeatherModel, WeatherForecast>();

        public PipelineEvent? LastAnalysis { get; init; }
    }

    protected override void ResolveInitialDependencies()
    {
        _pipelineRef = Context.GetActor<IForecastPipeline>();
        TrackDependency(_pipelineRef);
    }

    protected override void RequestSourceRefs()
    {
        var id = NextRequestId();
        _pipelineSourceRequestId = id;
        _pipelineRef!.Tell(new RequestPipelineOutput(id));
    }

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<IForecastPipeline>().PipeTo(Self,
            success: r => new PipelineResolved(r),
            failure: ex => new PipelineResolveFailed(ex));
    }

    protected override void ConfigureWaitingForRefs()
    {
        Receive<PipelineResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            var id = NextRequestId();
            _pipelineSourceRequestId = id;
            msg.Ref.Tell(new RequestPipelineOutput(id));
        });
        Receive<RequestPipelineOutputCompleted>(response =>
        {
            if (response.RequestId != _pipelineSourceRequestId) return;
            _pipelineSourceRef = response.SourceRef;
            _log.Debug("Pipeline SourceRef received from {Source}", Sender.Path);
            TryTransition();
        });
        Receive<RequestPipelineOutputFailed>(msg =>
        {
            if (msg.RequestId != _pipelineSourceRequestId) return;
            _log.Warning(msg.Cause, "Pipeline source request failed - retrying");
            ScheduleRetryResolve();
        });
        Receive<PipelineResolveFailed>(msg =>
        {
            _log.Warning(msg.Cause, "Failed to resolve ForecastPipeline - retrying");
            ScheduleRetryResolve();
        });

        ConfigureQueries();
    }

    protected override bool AllRefsReady() => _pipelineSourceRef is not null;

    protected override void MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var (hubSource, hubSink) = BroadcastHub.Sink<PipelineEvent>(bufferSize: 2)
            .PreMaterialize(Mat);

        _pipelineSourceRef!.Source
            .Via(killSwitch.Flow<PipelineEvent>())
            .Select(evt =>
            {
                UpdateState(evt);
                return evt;
            })
            .RunWith(hubSink, Mat);

        // A BroadcastHub without a consumer backpressures instead of dropping: this permanent drain keeps the
        // read model (and with it the pipeline and MQTT) consuming while no streaming client is attached.
        hubSource.RunWith(Sink.Ignore<PipelineEvent>(), Mat);

        _secondaryHubSource = hubSource;
    }

    protected override void ConfigureReady() => ConfigureQueries();

    private void ConfigureQueries()
    {
        Receive<QueryForecast>(HandleQueryForecast);
        Receive<QueryLatestAnalysis>(HandleQueryLatestAnalysis);
        Receive<RequestReadModelStream>(HandleStreamRequest);
    }

    private void HandleQueryForecast(QueryForecast msg)
    {
        if (_state.TryGetValue(msg.Location.Name, out var latest))
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
        Sender.Tell(_state.TryGetValue(msg.Location.Name, out var latest) && latest.LastAnalysis is { } analysis
            ? new LatestAnalysisResult(msg.Location, analysis)
            : new LatestAnalysisNotAvailable(msg.Location));
    }

    private void HandleStreamRequest(RequestReadModelStream msg)
    {
        if (_secondaryHubSource is null)
        {
            Sender.Tell(new RequestReadModelStreamFailed(msg.RequestId,
                new InvalidOperationException("Pipeline not materialized yet")));
            return;
        }

        _secondaryHubSource
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
                var forecastState = _state.GetValueOrDefault(forecast.Location.Name, new LatestResults());
                var forecasts = new Dictionary<WeatherModel, WeatherForecast>(forecastState.Forecasts)
                {
                    [forecast.Model] = forecast.Forecast
                };
                _state[forecast.Location.Name] = forecastState with { Forecasts = forecasts };
                break;
            case PipelineEvent.ConsensusComputed or PipelineEvent.AlertComputed
                or PipelineEvent.DerivedComputed or PipelineEvent.TrendComputed
                or PipelineEvent.IndexComputed:
                var location = evt switch
                {
                    PipelineEvent.ConsensusComputed c => c.Consensus.Location.Name,
                    PipelineEvent.AlertComputed a => a.Result.Location.Name,
                    PipelineEvent.DerivedComputed d => d.Result.Location.Name,
                    PipelineEvent.TrendComputed t => t.Result.Location.Name,
                    PipelineEvent.IndexComputed i => i.Result.Location.Name,
                    _ => throw new InvalidOperationException()
                };
                var analysisState = _state.GetValueOrDefault(location, new LatestResults());
                _state[location] = analysisState with { LastAnalysis = evt };
                break;
        }
    }

    protected override void OnDependencyLost()
    {
        _pipelineRef = null;
        _pipelineSourceRef = null;
        _pipelineSourceRequestId = 0;
        _secondaryHubSource = null;
    }
}
