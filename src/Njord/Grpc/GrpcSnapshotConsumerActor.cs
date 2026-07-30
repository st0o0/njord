using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Egress;
using Njord.Pipeline;
using Servus.Akka;

namespace Njord.Grpc;

public sealed class GrpcSnapshotConsumerActor : ReceiveActor, IWithStash
{
    private readonly ILogger<GrpcSnapshotConsumerActor> _logger;
    private readonly HashSet<IActorRef> _watchedDeps = [];
    private IMaterializer? _mat;
    private ISourceRef<EgressEvent>? _pendingSourceRef;
    private IActorRef? _lastTerminatedRef;
    private int _retryCount;
    private SharedKillSwitch _killSwitch = KillSwitches.Shared("stream-kill");

    public IStash Stash { get; set; } = null!;

    private sealed record EgressResolved(IActorRef Ref);
    private sealed record RetryResolve;
    private sealed record SnapshotActorsResolved(IActorRef Forecast, IActorRef Enrichment);

    public GrpcSnapshotConsumerActor(ILogger<GrpcSnapshotConsumerActor> logger)
    {
        _logger = logger;
        WaitingForSource();
    }

    protected override void PreStart()
    {
        _mat = Context.Materializer();
        RequestEgressSource();
    }

    private void RequestEgressSource()
    {
        Context.GetActorAsync<EgressActor>().PipeTo(Self, success: r => new EgressResolved(r));
    }

    private void WaitingForSource()
    {
        Receive<RetryResolve>(_ =>
        {
            _lastTerminatedRef = null;
            RequestEgressSource();
        });
        Receive<EgressResolved>(msg =>
        {
            if (Equals(msg.Ref, _lastTerminatedRef))
            {
                ScheduleRetryResolve();
                return;
            }

            _watchedDeps.Add(msg.Ref);
            Context.Watch(msg.Ref);
            msg.Ref.Tell(new RequestEgressSource());
        });
        Receive<EgressSourceResponse>(response =>
        {
            if (_lastTerminatedRef is not null)
            {
                return;
            }

            _pendingSourceRef = response.SourceRef;

            var forecastTask = Context.GetActorAsync<ForecastSnapshotActor>();
            var enrichmentTask = Context.GetActorAsync<EnrichmentSnapshotActor>();
            Task.WhenAll(forecastTask, enrichmentTask)
                .PipeTo(Self, success: _ => new SnapshotActorsResolved(forecastTask.Result, enrichmentTask.Result));

            Become(WaitingForSnapshotActors);
        });
        Receive<Terminated>(OnTerminated);
        ReceiveAny(_ => Stash.Stash());
    }

    private void WaitingForSnapshotActors()
    {
        Receive<SnapshotActorsResolved>(msg =>
        {
            MaterializeGraph(_pendingSourceRef!, msg.Forecast, msg.Enrichment);
            _pendingSourceRef = null;
            _logger.LogInformation("gRPC snapshot consumer materialized — capturing forecasts and enrichments");
            Become(Ready);
            Stash.UnstashAll();
        });
        Receive<Terminated>(OnTerminated);
        ReceiveAny(_ => Stash.Stash());
    }

    private void Ready()
    {
        Receive<Terminated>(OnTerminated);
    }

    private void OnTerminated(Terminated msg)
    {
        if (!_watchedDeps.Remove(msg.ActorRef))
        {
            return;
        }

        _logger.LogWarning("Watched actor {Actor} terminated — re-requesting source", msg.ActorRef.Path.Name);
        _lastTerminatedRef = msg.ActorRef;
        _retryCount = 0;
        _pendingSourceRef = null;
        _killSwitch.Shutdown();
        _killSwitch = KillSwitches.Shared("stream-kill");
        RequestEgressSource();
        Become(WaitingForSource);
    }

    private void MaterializeGraph(ISourceRef<EgressEvent> sourceRef, IActorRef forecastActor, IActorRef enrichmentActor)
    {
        sourceRef.Source
            .Via(_killSwitch.Flow<EgressEvent>())
            .SelectAsync(1, async update => update switch
            {
                EgressEvent.PerModelUpdate pmu =>
                    await forecastActor.Ask<Ack>(
                        new UpdateForecast(pmu.Location, pmu.Model, pmu.Forecast))
                    is var _ ? update : update,

                EgressEvent.EnrichmentUpdate eu =>
                    await enrichmentActor.Ask<Ack>(
                        new UpdateEnrichment(eu.Location, eu.TypeName, eu.Result))
                    is var _ ? update : update,

                _ => update,
            })
            .WithAttributes(ActorAttributes.CreateSupervisionStrategy(StreamSupervision.LoggingDecider(_logger)))
            .To(Sink.Ignore<EgressEvent>())
            .Run(_mat!);
    }

    private void ScheduleRetryResolve()
    {
        var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, _retryCount), 30));
        _retryCount++;
        Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, new RetryResolve(), Self);
    }
}
