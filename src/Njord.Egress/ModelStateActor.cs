using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Messages.Egress;
using Njord.Weather;
using Servus.Akka;

namespace Njord.Egress;

public sealed class ModelStateActor : StreamConsumerActor
{
    private readonly IReadOnlyList<int> _horizons;
    private readonly int _forecastDays;
    private readonly ResolvedParameterSet _parameters;
    private ILoggingAdapter _log = null!;

    private IActorRef? _pipelineRef;
    private ISourceRef<FetchOutcome>? _sourceRef;
    private long _pipelineSourceRequestId;
    private Source<EgressEvent, NotUsed>? _broadcastHubSource;

    private sealed record PipelineResolved(IActorRef Ref);
    private sealed record PipelineResolveFailed(Exception Cause);

    public ModelStateActor(
        IOptions<NjordOptions> options,
        ResolvedParameterSet parameters)
    {
        var opts = options.Value;
        _horizons = [.. opts.Horizons];
        _forecastDays = opts.ForecastDays;
        _parameters = parameters;
    }

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
        _pipelineRef!.Tell(new RequestPipelineSource(id));
    }

    protected override void ResolveDependencies()
    {
        Context.GetActorAsync<IPipelineActor>().PipeTo(Self, success: r => new PipelineResolved(r), failure: ex => new PipelineResolveFailed(ex));
    }

    protected override void ConfigureWaitingForRefs()
    {
        Receive<PipelineResolved>(msg =>
        {
            if (IsDeadRef(msg.Ref)) { ScheduleRetryResolve(); return; }
            TrackDependency(msg.Ref);
            var id = NextRequestId();
            _pipelineSourceRequestId = id;
            msg.Ref.Tell(new RequestPipelineSource(id));
        });
        Receive<PipelineSourceResponse>(response =>
        {
            if (response.RequestId != _pipelineSourceRequestId) return;
            _sourceRef = response.SourceRef;
            _log.Debug("SourceRef received from {Source}", Sender.Path);
            TryTransition();
        });
        Receive<PipelineSourceFailed>(msg =>
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
    }

    protected override bool AllRefsReady() => _sourceRef is not null;

    protected override void MaterializeGraph(SharedKillSwitch killSwitch)
    {
        var log = _log;
        var tracker = new CapabilityTracker(_parameters, _horizons, _forecastDays);

        var (broadcastHubSource, broadcastHubSink) = BroadcastHub.Sink<EgressEvent>(bufferSize: 4)
            .PreMaterialize(Mat);

        _broadcastHubSource = broadcastHubSource;

        _sourceRef!.Source
            .Via(killSwitch.Flow<FetchOutcome>())
            .Log("egress-in", o => o switch
            {
                FetchOutcome.Success s => $"OK {s.Forecast.Location}/{s.Forecast.Model.Id}",
                FetchOutcome.Failure f => $"FAIL {f.Location}/{f.Model.Id}",
                _ => "?",
            }, _log)
            .Collect(outcome => outcome is FetchOutcome.Success, outcome => (FetchOutcome.Success)outcome)
            .SelectMany(success =>
            {
                var forecast = success.Forecast;
                var events = new List<EgressEvent>();

                var cap = tracker.Track(forecast);
                if (cap is not null)
                {
                    log.Info("Capability learned for {Location}/{Model}: {ParamCount} parameters, {HorizonCount} horizons",
                        forecast.Location, forecast.Model.Id, cap.SupportedParameters.Count, cap.ApplicableHorizons.Count);
                    events.Add(cap);
                }

                events.Add(new EgressEvent.PerModelUpdate(forecast.Location, forecast.Model, forecast));
                return events;
            })
            .Log("egress-out", e => e switch
            {
                EgressEvent.CapabilityLearned c => $"cap {c.Location}/{c.Model.Id}",
                EgressEvent.PerModelUpdate u => $"model {u.Location}/{u.Model.Id}",
                _ => "?",
            }, _log)
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_log)))
            .To(broadcastHubSink)
            .Run(Mat);
    }

    protected override void ConfigureReady()
    {
        Receive<RequestModelStateSource>(msg =>
        {
            _broadcastHubSource!
                .RunWith(StreamRefs.SourceRef<EgressEvent>(), Mat)
                .PipeTo(Sender, Self,
                    sr => new ModelStateSourceResponse(msg.RequestId, sr),
                    ex =>
                    {
                        _log.Error(ex, "Failed to create ModelState SourceRef");
                        return new ModelStateSourceFailed(msg.RequestId, ex);
                    });
        });
    }

    protected override void OnDependencyLost()
    {
        _pipelineRef = null;
        _sourceRef = null;
        _pipelineSourceRequestId = 0;
        _broadcastHubSource = null;
    }
}
