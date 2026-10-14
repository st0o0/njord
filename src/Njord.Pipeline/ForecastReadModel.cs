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
using Servus.Akka;
using WeatherForecast = Njord.Weather.Forecast;

namespace Njord.Pipeline;

// In-memory projection of the pipeline output for the API: the latest forecast per location and model,
// the latest event of each analysis kind per location, and a secondary hub for streaming clients.
public sealed class ForecastReadModel : StreamConsumerActor
{
    // Per streaming client; a client that falls behind loses its oldest events instead of holding up the hub.
    private const int StreamClientBufferSize = 256;

    private readonly ILoggingAdapter _log = Context.GetLogger();

    private IActorRef? _pipelineRef;
    private ISourceRef<PipelineEvent>? _pipelineSourceRef;
    private long _pipelineSourceRequestId;

    private Sink<PipelineEvent, NotUsed>? _clientHubSink;
    private Source<PipelineEvent, NotUsed>? _secondaryHubSource;

    // Keyed by LocationId, whose equality ignores case, so a query matches whatever spelling it uses.
    // Written only by the materialized stream and read by the actor: an immutable map swapped as a whole.
    private volatile ImmutableDictionary<LocationId, LatestResults> _state = ImmutableDictionary<LocationId, LatestResults>.Empty;

    private sealed record PipelineResolved(IActorRef Ref);
    private sealed record PipelineResolveFailed(Exception Cause);

    private sealed record LatestResults
    {
        public IReadOnlyDictionary<WeatherModel, WeatherForecast> Forecasts { get; init; } =
            new Dictionary<WeatherModel, WeatherForecast>();

        public LatestAnalysisResult? Analysis { get; init; }
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

    protected override Task MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var clientHubSink = _clientHubSink ?? MaterializeClientHub();

        return _pipelineSourceRef!.Source
            .Via(killSwitch.Flow<PipelineEvent>())
            .Select(evt =>
            {
                UpdateState(evt);
                return evt;
            })
            .WatchTermination(Keep.Right)
            // The termination above already reports a failed subscription to the base class; it must not fail the
            // MergeHub, which outlives the subscription so streaming clients keep their streams across a rewire.
            .Recover(_ => Option<PipelineEvent>.None)
            .To(clientHubSink)
            .Run(Mat);
    }

    // Once per actor incarnation: every pipeline subscription feeds the same MergeHub, so the secondary hub (and
    // the SourceRefs of streaming clients) survives a rewire and a pipeline restart.
    private Sink<PipelineEvent, NotUsed> MaterializeClientHub()
    {
        var (mergeSink, mergeSource) = MergeHub.Source<PipelineEvent>(perProducerBufferSize: 16).PreMaterialize(Mat);
        var hubSource = mergeSource.RunWith(BroadcastHub.Sink<PipelineEvent>(bufferSize: 2), Mat);

        // A BroadcastHub without a consumer backpressures instead of dropping: this permanent drain keeps the
        // read model (and with it the pipeline and MQTT) consuming while no streaming client is attached.
        hubSource.RunWith(Sink.Ignore<PipelineEvent>(), Mat);

        _clientHubSink = mergeSink;
        _secondaryHubSource = hubSource;
        return mergeSink;
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

    // The latest values and the secondary hub are kept: queries and streaming clients ride out the gap.
    protected override void OnDependencyLost()
    {
        _pipelineRef = null;
        _pipelineSourceRef = null;
        _pipelineSourceRequestId = 0;
    }

    protected override void OnStreamLost()
    {
        _pipelineSourceRef = null;
        _pipelineSourceRequestId = 0;
    }
}
