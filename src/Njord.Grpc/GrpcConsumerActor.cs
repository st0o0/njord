using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core.Actors;
using Njord.Messages.Egress;
using Njord.Messages.Snapshots;
using Njord.Weather;
using Servus.Akka;

namespace Njord.Grpc;

public sealed class GrpcConsumerActor : StreamConsumerActor
{
    private ILoggingAdapter _log = null!;

    private IActorRef? _pipelineRef;
    private ISourceRef<PipelineEvent>? _pipelineSourceRef;
    private long _pipelineSourceRequestId;

    private Source<PipelineEvent, NotUsed>? _secondaryHubSource;
    private readonly Dictionary<string, LatestResults> _state = new();

    private sealed record PipelineResolved(IActorRef Ref);
    private sealed record PipelineResolveFailed(Exception Cause);

    public sealed record QueryLatestResults(string Location);
    public sealed record LatestResults
    {
        public IReadOnlyDictionary<WeatherModel, ModelForecast> Forecasts { get; init; } =
            new Dictionary<WeatherModel, ModelForecast>();

        public PipelineEvent.AnalysisResult? LastAnalysis { get; init; }
    }
    public sealed record RequestGrpcStreamSource(long RequestId);
    public sealed record GrpcStreamSourceResponse(long RequestId, ISourceRef<PipelineEvent> SourceRef);
    public sealed record GrpcStreamSourceFailed(long RequestId, Exception Cause);

    protected override void PreStart()
    {
        _log = Context.GetLogger();
        base.PreStart();
    }

    protected override void ResolveInitialDependencies()
    {
        _pipelineRef = Context.GetActor<IPipelineActor>();
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
        Context.GetActorAsync<IPipelineActor>().PipeTo(Self,
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
        Receive<PipelineOutputResponse>(response =>
        {
            if (response.RequestId != _pipelineSourceRequestId) return;
            _pipelineSourceRef = response.SourceRef;
            _log.Debug("Pipeline SourceRef received from {Source}", Sender.Path);
            TryTransition();
        });
        Receive<PipelineOutputFailed>(msg =>
        {
            if (msg.RequestId != _pipelineSourceRequestId) return;
            _log.Warning(msg.Cause, "Pipeline source request failed - retrying");
            ScheduleRetryResolve();
        });
        Receive<PipelineResolveFailed>(msg =>
        {
            _log.Warning(msg.Cause, "Failed to resolve PipelineActor - retrying");
            ScheduleRetryResolve();
        });

        Receive<QueryLatestResults>(msg =>
        {
            Sender.Tell(_state.GetValueOrDefault(msg.Location, new LatestResults()));
        });
        Receive<QueryForecast>(HandleQueryForecast);
        Receive<RequestGrpcStreamSource>(HandleStreamSourceRequest);
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

        _secondaryHubSource = hubSource;
    }

    protected override void ConfigureReady()
    {
        Receive<QueryLatestResults>(msg =>
        {
            Sender.Tell(_state.GetValueOrDefault(msg.Location, new LatestResults()));
        });
        Receive<QueryForecast>(HandleQueryForecast);
        Receive<RequestGrpcStreamSource>(HandleStreamSourceRequest);
    }

    private void HandleQueryForecast(QueryForecast msg)
    {
        if (_state.TryGetValue(msg.Location, out var latest))
        {
            foreach (var (model, forecast) in latest.Forecasts)
            {
                if (string.Equals(model.Id, msg.ModelId, StringComparison.OrdinalIgnoreCase))
                {
                    Sender.Tell(new QueryForecastCompleted(forecast));
                    return;
                }
            }
        }

        Sender.Tell(new QueryForecastNotAvailable($"{msg.Location}|{msg.ModelId}"));
    }

    private void HandleStreamSourceRequest(RequestGrpcStreamSource msg)
    {
        if (_secondaryHubSource is null)
        {
            Sender.Tell(new GrpcStreamSourceFailed(msg.RequestId,
                new InvalidOperationException("Pipeline not materialized yet")));
            return;
        }

        _secondaryHubSource
            .RunWith(StreamRefs.SourceRef<PipelineEvent>(), Mat)
            .PipeTo(Sender, Self,
                sr => new GrpcStreamSourceResponse(msg.RequestId, sr),
                ex =>
                {
                    _log.Error(ex, "Failed to create gRPC stream SourceRef");
                    return new GrpcStreamSourceFailed(msg.RequestId, ex);
                });
    }

    private void UpdateState(PipelineEvent evt)
    {
        switch (evt)
        {
            case PipelineEvent.ModelForecastUpdate forecast:
                var forecastState = _state.GetValueOrDefault(forecast.Location, new LatestResults());
                var forecasts = new Dictionary<WeatherModel, ModelForecast>(forecastState.Forecasts)
                {
                    [forecast.Model] = forecast.Forecast
                };
                _state[forecast.Location] = forecastState with { Forecasts = forecasts };
                break;
            case PipelineEvent.AnalysisResult analysis:
                var analysisState = _state.GetValueOrDefault(analysis.Location, new LatestResults());
                _state[analysis.Location] = analysisState with { LastAnalysis = analysis };
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
