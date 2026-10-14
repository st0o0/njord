using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Servus.Resilience;

namespace Njord.Core.Actors;

public abstract class StreamConsumerActor : ReceiveActor, IWithStash
{
    internal static readonly BackoffPolicy DefaultRetryPolicy =
        Backoff.Create(TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromSeconds(30));

    // Twice the backoff cap: a graph that survived this long counts as healthy again.
    internal static readonly TimeSpan DefaultGraphStableAfter = TimeSpan.FromSeconds(60);

    private readonly BackoffPolicy _retryPolicy;
    private readonly TimeSpan _graphStableAfter;
    private readonly HashSet<IActorRef> _watchedDeps = [];
    private IActorRef? _lastTerminatedRef;
    private int _retryCount;
    private bool _retryPending;
    private long _requestId;
    private SharedKillSwitch _killSwitch = KillSwitches.Shared("stream-kill");
    private long _graphGeneration;
    private int _graphLossCount;
    private readonly ILoggingAdapter _log = Context.GetLogger();

    public IStash Stash { get; set; } = null!;

    private sealed record RetryResolve;
    private sealed record RecoverAfterTerminated;
    private sealed record GraphCompleted(long Generation);
    private sealed record GraphFailed(long Generation, Exception Cause);
    private sealed record RewireGraph(long Generation);
    private sealed record GraphStable(long Generation);

    protected IMaterializer Mat { get; private set; } = null!;

    protected virtual string ConsumerName => GetType().Name;

    protected StreamConsumerActor(BackoffPolicy? retryPolicy = null, TimeSpan? graphStableAfter = null)
    {
        _retryPolicy = retryPolicy ?? DefaultRetryPolicy;
        _graphStableAfter = graphStableAfter ?? DefaultGraphStableAfter;
        ReceiveAny(_ => Stash.Stash());
    }

    protected override void PreStart()
    {
        Mat = Context.Materializer();
        ResolveInitialDependencies();
        EnterWaitingForRefs();
        RequestSourceRefs();
    }

    // A graph ended by the actor's own stop must not count as lost.
    protected override void PostStop()
    {
        _graphGeneration++;
        _killSwitch.Shutdown();
        base.PostStop();
    }

    protected abstract void ResolveInitialDependencies();

    protected abstract void RequestSourceRefs();

    protected abstract void ResolveDependencies();

    protected abstract bool AllRefsReady();

    // Returns the completion of the materialized graph: when it completes or fails, the base rewires.
    protected abstract Task MaterializeGraph(SharedKillSwitch killSwitch);

    protected abstract void ConfigureWaitingForRefs();

    protected virtual void ConfigureReady() { }

    protected virtual void OnDependencyLost() { }

    // The graph ended while its dependencies are alive: drop the SourceRefs so they are requested again.
    protected virtual void OnStreamLost() { }

    protected long NextRequestId() => ++_requestId;

    protected void TrackDependency(IActorRef dep)
    {
        _watchedDeps.Add(dep);
        Context.Watch(dep);
    }

    protected bool IsDeadRef(IActorRef dep) => Equals(dep, _lastTerminatedRef);

    protected void ScheduleRetryResolve()
    {
        if (_retryPending)
        {
            return;
        }

        _retryPending = true;
        var delay = _retryPolicy.DelayWithJitter(_retryCount);
        _retryCount++;
        Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, new RetryResolve(), Self);
    }

    protected void TryTransition()
    {
        if (!AllRefsReady())
        {
            return;
        }

        _lastTerminatedRef = null;
        _retryCount = 0;
        _retryPending = false;
        var generation = ++_graphGeneration;
        Task completion;
        try
        {
            completion = MaterializeGraph(_killSwitch);
        }
        catch (Exception ex)
        {
            completion = Task.FromException(ex);
        }

        completion.PipeTo(Self,
            success: () => new GraphCompleted(generation),
            failure: ex => new GraphFailed(generation, ex));
        Context.System.Scheduler.ScheduleTellOnceCancelable(_graphStableAfter, Self, new GraphStable(generation), Self);
        EnterReady();
        Stash.UnstashAll();
    }

    private void EnterWaitingForRefs()
    {
        _retryPending = false;
        Become(WaitingForRefsBehavior);
    }

    private void WaitingForRefsBehavior()
    {
        Receive<RetryResolve>(_ =>
        {
            _retryPending = false;
            _lastTerminatedRef = null;
            ResolveDependencies();
        });
        Receive<RecoverAfterTerminated>(_ =>
        {
            OnDependencyLost();
            ResolveDependencies();
        });
        Receive<RewireGraph>(msg =>
        {
            if (msg.Generation == _graphGeneration)
            {
                RequestSourceRefs();
            }
        });
        ReceiveGraphLifecycle();
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
        Receive<RecoverAfterTerminated>(_ =>
        {
            OnDependencyLost();
            EnterWaitingForRefs();
            ResolveDependencies();
        });
        Receive<RewireGraph>(_ => { });
        ReceiveGraphLifecycle();
    }

    // Only the graph of the current generation counts; in WaitingForRefs every completion is stale.
    private void ReceiveGraphLifecycle()
    {
        Receive<GraphCompleted>(msg => OnGraphEnded(msg.Generation, null));
        Receive<GraphFailed>(msg => OnGraphEnded(msg.Generation, msg.Cause));
        Receive<GraphStable>(msg =>
        {
            if (msg.Generation == _graphGeneration)
            {
                _graphLossCount = 0;
            }
        });
    }

    private void OnGraphEnded(long generation, Exception? cause)
    {
        if (generation != _graphGeneration)
        {
            return;
        }

        var delay = _retryPolicy.DelayWithJitter(_graphLossCount);
        _graphLossCount++;
        if (cause is null)
        {
            _log.Warning("{0} stream completed unexpectedly - requesting new SourceRefs in {1}", ConsumerName, delay);
        }
        else
        {
            _log.Warning(cause, "{0} stream failed - requesting new SourceRefs in {1}", ConsumerName, delay);
        }

        Telemetry.StreamConsumerRewires.Add(1, new KeyValuePair<string, object?>("consumer", ConsumerName));

        var rewireGeneration = ++_graphGeneration;
        _killSwitch.Shutdown();
        _killSwitch = KillSwitches.Shared("stream-kill");
        _requestId++;
        OnStreamLost();
        EnterWaitingForRefs();
        Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, new RewireGraph(rewireGeneration), Self);
    }

    private void HandleTerminated(Terminated msg)
    {
        if (!_watchedDeps.Remove(msg.ActorRef))
        {
            return;
        }

        _graphGeneration++;
        _killSwitch.Shutdown();
        _killSwitch = KillSwitches.Shared("stream-kill");
        _lastTerminatedRef = msg.ActorRef;
        _retryCount = 0;
        _requestId++;

        Self.Tell(new RecoverAfterTerminated());
    }
}
