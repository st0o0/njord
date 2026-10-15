using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Messages.Pipeline;
using Servus.Resilience;

namespace Njord.Core.Actors;

// Consumes the pipeline output through one SourceRef at a time. It attaches when it starts and re-attaches only when
// the pipeline incarnation that answered terminates or its own graph ends; every attach attempt ends with a reply, a
// failure or the reply timeout. Subclass messages are handled at any time: there is no waiting phase.
public abstract class StreamConsumerActor : ReceiveActor, IWithTimers
{
    internal static readonly BackoffPolicy DefaultRetryPolicy =
        Backoff.Create(TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromSeconds(30));

    internal static readonly TimeSpan DefaultReplyTimeout = TimeSpan.FromSeconds(10);

    // Twice the backoff cap: a subscription that survived this long counts as healthy again.
    internal static readonly TimeSpan DefaultSubscriptionStableAfter = TimeSpan.FromSeconds(60);

    private const string AttachTimer = "attach";
    private const string ReplyTimer = "reply";
    private const string StableTimer = "stable";

    private readonly BackoffPolicy _retryPolicy;
    private readonly TimeSpan _replyTimeout;
    private readonly TimeSpan _subscriptionStableAfter;

    private long _lastRequestId;
    private long _pendingRequestId;
    private long _subscriptionId;
    private int _attempts;
    private IActorRef? _pipeline;
    private SharedKillSwitch? _killSwitch;
    private readonly ILoggingAdapter _log = Context.GetLogger();

    private sealed record Attach;
    private sealed record PipelineResolved(long RequestId, IActorRef Pipeline);
    private sealed record PipelineResolveFailed(long RequestId, Exception Cause);
    private sealed record ReplyTimedOut(long RequestId);
    private sealed record SubscriptionEnded(long SubscriptionId, Exception? Cause) : IDeadLetterSuppression;
    private sealed record SubscriptionStable(long SubscriptionId);

    public ITimerScheduler Timers { get; set; } = null!;

    protected IMaterializer Mat { get; private set; } = null!;

    protected virtual string ConsumerName => GetType().Name;

    protected StreamConsumerActor(BackoffPolicy? retryPolicy = null, TimeSpan? replyTimeout = null,
        TimeSpan? subscriptionStableAfter = null)
    {
        _retryPolicy = retryPolicy ?? DefaultRetryPolicy;
        _replyTimeout = replyTimeout ?? DefaultReplyTimeout;
        _subscriptionStableAfter = subscriptionStableAfter ?? DefaultSubscriptionStableAfter;

        Receive<Attach>(_ => StartAttach());
        Receive<PipelineResolved>(OnPipelineResolved);
        Receive<PipelineResolveFailed>(msg =>
            OnAttemptFailed(msg.RequestId, "the pipeline could not be resolved", msg.Cause));
        Receive<RequestPipelineOutputCompleted>(OnSourceRef);
        Receive<RequestPipelineOutputFailed>(msg =>
            OnAttemptFailed(msg.RequestId, "the pipeline could not create a SourceRef", msg.Cause));
        Receive<ReplyTimedOut>(msg =>
            OnAttemptFailed(msg.RequestId, $"no SourceRef within {_replyTimeout}", null));
        Receive<SubscriptionEnded>(OnSubscriptionEnded);
        Receive<SubscriptionStable>(msg =>
        {
            if (msg.SubscriptionId == _subscriptionId)
            {
                _attempts = 0;
            }
        });
        Receive<Terminated>(msg =>
        {
            if (_subscriptionId != 0 && Equals(msg.ActorRef, _pipeline))
            {
                LoseSubscription("the pipeline terminated", null);
            }
        });
    }

    protected override void PreStart()
    {
        Mat = Context.Materializer();
        StartAttach();
    }

    protected override void PostStop()
    {
        _subscriptionId = 0;
        _killSwitch?.Shutdown();
        base.PostStop();
    }

    // Runs the consumer's graph on the subscription's events and returns its completion.
    protected abstract Task MaterializeGraph(Source<PipelineEvent, NotUsed> events);

    // One timer bounds both the registry wait and the pipeline's reply.
    private void StartAttach()
    {
        var requestId = _pendingRequestId = ++_lastRequestId;
        Timers.StartSingleTimer(ReplyTimer, new ReplyTimedOut(requestId), _replyTimeout);
        ActorRegistry.For(Context.System).GetAsync<IForecastPipeline>().PipeTo(Self,
            success: pipeline => new PipelineResolved(requestId, pipeline),
            failure: ex => new PipelineResolveFailed(requestId, ex));
    }

    private void OnPipelineResolved(PipelineResolved msg)
    {
        if (msg.RequestId == _pendingRequestId)
        {
            msg.Pipeline.Tell(new RequestPipelineOutput(msg.RequestId));
        }
    }

    private void OnSourceRef(RequestPipelineOutputCompleted msg)
    {
        if (msg.RequestId != _pendingRequestId)
        {
            return;
        }

        _pendingRequestId = 0;
        Timers.Cancel(ReplyTimer);
        _log.Debug("Pipeline SourceRef received from {Source}", Sender.Path);

        // The sender is the pipeline incarnation, not the registry's proxy: its termination is a pipeline restart.
        _pipeline = Sender;
        Context.Watch(_pipeline);
        var subscriptionId = _subscriptionId = msg.RequestId;
        _killSwitch = KillSwitches.Shared($"{ConsumerName}-subscription-{subscriptionId}");

        Task completion;
        try
        {
            completion = MaterializeGraph(msg.SourceRef.Source.Via(_killSwitch.Flow<PipelineEvent>()));
        }
        catch (Exception ex)
        {
            completion = Task.FromException(ex);
        }

        completion.PipeTo(Self,
            success: () => new SubscriptionEnded(subscriptionId, null),
            failure: ex => new SubscriptionEnded(subscriptionId, ex));
        Timers.StartSingleTimer(StableTimer, new SubscriptionStable(subscriptionId), _subscriptionStableAfter);
    }

    private void OnAttemptFailed(long requestId, string reason, Exception? cause)
    {
        if (requestId != _pendingRequestId)
        {
            return;
        }

        _pendingRequestId = 0;
        Timers.Cancel(ReplyTimer);
        ScheduleAttach($"{ConsumerName} could not attach to the pipeline: {reason}", cause);
    }

    private void OnSubscriptionEnded(SubscriptionEnded msg)
    {
        if (msg.SubscriptionId != _subscriptionId)
        {
            return;
        }

        LoseSubscription(msg.Cause is null ? "its stream completed unexpectedly" : "its stream failed", msg.Cause);
    }

    // Either signal of a lost subscription (pipeline terminated, graph ended) arrives first; the other finds no
    // current subscription and is ignored, so every loss is counted once.
    private void LoseSubscription(string reason, Exception? cause)
    {
        _subscriptionId = 0;
        Timers.Cancel(StableTimer);
        if (_pipeline is not null)
        {
            Context.Unwatch(_pipeline);
            _pipeline = null;
        }

        _killSwitch?.Shutdown();
        _killSwitch = null;
        Telemetry.StreamConsumerRewires.Add(1, new KeyValuePair<string, object?>("consumer", ConsumerName));
        ScheduleAttach($"{ConsumerName} lost its pipeline subscription: {reason}", cause);
    }

    private void ScheduleAttach(string reason, Exception? cause)
    {
        var delay = _retryPolicy.DelayWithJitter(_attempts);
        _attempts++;
        _log.Warning(cause, "{0} - attaching again in {1}", reason, delay);
        Timers.StartSingleTimer(AttachTimer, new Attach(), delay);
    }
}
