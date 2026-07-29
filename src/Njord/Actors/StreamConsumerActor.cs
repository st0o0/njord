using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace Njord.Actors;

public abstract class StreamConsumerActor : ReceiveActor, IWithStash
{
    private readonly HashSet<IActorRef> _watchedDeps = [];
    private IActorRef? _lastTerminatedRef;
    private int _retryCount;
    private SharedKillSwitch _killSwitch = KillSwitches.Shared("stream-kill");

    public IStash Stash { get; set; } = null!;

    private sealed record RetryResolve;

    protected IMaterializer Mat { get; private set; } = null!;

    protected StreamConsumerActor()
    {
        ReceiveAny(_ => Stash.Stash());
    }

    protected override void PreStart()
    {
        Mat = Context.Materializer();
        EnterWaitingForRefs();
        ResolveDependencies();
    }

    protected abstract void ResolveDependencies();

    protected abstract bool AllRefsReady();

    protected abstract void MaterializeGraph(SharedKillSwitch killSwitch);

    protected abstract void ConfigureWaitingForRefs();

    protected virtual void ConfigureReady() { }

    protected virtual void OnDependencyLost() { }

    protected void TrackDependency(IActorRef dep)
    {
        _watchedDeps.Add(dep);
        Context.Watch(dep);
    }

    protected bool IsDeadRef(IActorRef dep) => Equals(dep, _lastTerminatedRef);

    protected void ScheduleRetryResolve()
    {
        var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, _retryCount), 30));
        _retryCount++;
        Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, new RetryResolve(), Self);
    }

    protected void TryTransition()
    {
        if (!AllRefsReady() || _lastTerminatedRef is not null)
            return;

        _retryCount = 0;
        MaterializeGraph(_killSwitch);
        EnterReady();
        Stash.UnstashAll();
    }

    private void EnterWaitingForRefs()
    {
        Become(WaitingForRefsBehavior);
    }

    private void WaitingForRefsBehavior()
    {
        Receive<RetryResolve>(_ =>
        {
            _lastTerminatedRef = null;
            ResolveDependencies();
        });
        ConfigureWaitingForRefs();
        Receive<Terminated>(HandleTerminated);
        ReceiveAny(_ => Stash.Stash());
    }

    private void EnterReady()
    {
        Become(ReadyBehavior);
    }

    private void ReadyBehavior()
    {
        ConfigureReady();
        Receive<Terminated>(HandleTerminated);
    }

    private void HandleTerminated(Terminated msg)
    {
        if (!_watchedDeps.Remove(msg.ActorRef))
            return;

        _killSwitch.Shutdown();
        _killSwitch = KillSwitches.Shared("stream-kill");
        _lastTerminatedRef = msg.ActorRef;
        _retryCount = 0;

        OnDependencyLost();
        ResolveDependencies();
        EnterWaitingForRefs();
    }
}
