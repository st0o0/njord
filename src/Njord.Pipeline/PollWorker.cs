using Akka.Actor;
using Akka.Event;
using Akka.Persistence;
using Njord.Core.Actors;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Polling;
using Njord.Weather;
using Servus.Akka;

namespace Njord.Pipeline;

// One persistent entity per (location, model) target in the poll shard region. It owns the target's poll state,
// its timer and at most one fetch request in flight, and reports every start and outcome to the poll manager.
public sealed class PollWorker : ReceivePersistentActor, IWithTimers
{
    private const int SnapshotInterval = 50;
    private const string PollTimerKey = "poll";

    private sealed record PollDue
    {
        public static readonly PollDue Instance = new();
    }

    private sealed record FetchReplied(Guid RequestId, FetchForecastResponse Reply);

    private readonly TimeProvider _timeProvider;
    private readonly FetchTimeouts _fetchTimeouts;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private PollWorkerState _state = PollWorkerState.Empty;
    private Guid? _inFlight;

    public ITimerScheduler Timers { get; set; } = null!;

    public override string PersistenceId { get; }

    public PollWorker(TimeProvider timeProvider, FetchTimeouts fetchTimeouts, string entityId)
    {
        _timeProvider = timeProvider;
        _fetchTimeouts = fetchTimeouts;
        PersistenceId = $"poll-worker-{entityId}";

        Recover<PollingStarted>(evt => _state = _state.Apply(evt));
        Recover<PollingStopped>(evt => _state = _state.Apply(evt));
        Recover<ForecastChanged>(evt => _state = _state.Apply(evt));
        Recover<ForecastUnchanged>(evt => _state = _state.Apply(evt));
        Recover<PollFailed>(evt => _state = _state.Apply(evt));
        Recover<PollDeferred>(evt => _state = _state.Apply(evt));
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is PersistedPollWorkerState snapshot)
            {
                _state = snapshot.FromPersistence();
            }
        });
        Recover<RecoveryCompleted>(_ => OnRecovered());

        Command<StartPolling>(OnStartPolling);
        Command<StopPolling>(_ => OnStopPolling());
        Command<TriggerTargetPoll>(_ => Poll("trigger"));
        Command<PollDue>(_ => Poll("timer"));
        Command<FetchReplied>(OnFetchReplied);
        Command<SaveSnapshotSuccess>(success =>
        {
            _log.Debug("Snapshot saved at sequence {SequenceNr}, cleaning journal", success.Metadata.SequenceNr);
            DeleteMessages(success.Metadata.SequenceNr);
            DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr - 1));
        });
        Command<SaveSnapshotFailure>(fail =>
            _log.Warning(fail.Cause, "Snapshot save failed for {PersistenceId}", PersistenceId));
        Command<DeleteMessagesFailure>(fail =>
            _log.Warning(fail.Cause, "Delete messages failed for {PersistenceId}", PersistenceId));
        Command<DeleteSnapshotsFailure>(fail =>
            _log.Warning(fail.Cause, "Delete snapshots failed for {PersistenceId}", PersistenceId));
        Command<DeleteMessagesSuccess>(_ => { });
        Command<DeleteSnapshotsSuccess>(_ => { });
    }

    // A worker restarted by its shard, or started again after it stopped, resumes on its own.
    private void OnRecovered()
    {
        if (!_state.Active)
        {
            return;
        }

        ScheduleNext();
        Report(PollWorkerOutcome.Planned);
    }

    private void OnStartPolling(StartPolling cmd)
    {
        if (_state is { Active: true, Target: not null } && _state.DiscoveryInterval == cmd.DiscoveryInterval)
        {
            Report(PollWorkerOutcome.Planned);
            return;
        }

        var previous = _state.DiscoveryInterval;
        var wasStarted = _state.Target is not null;
        var evt = new PollingStarted(cmd.Target.Location.Name, cmd.Target.ModelId, cmd.DiscoveryInterval, _timeProvider.GetUtcNow());
        Persist(evt, e =>
        {
            _state = _state.Apply(e);
            if (wasStarted && previous != e.DiscoveryInterval)
            {
                _log.Debug("Discovery interval of {Location}/{Model} changed from {Previous} to {Current}",
                    e.Location, e.ModelId, previous, e.DiscoveryInterval);
            }

            ScheduleNext();
            Report(PollWorkerOutcome.Planned);
            MaybeSnapshot();
        });
    }

    private void OnStopPolling()
    {
        Timers.CancelAll();
        if (!_state.Active)
        {
            Context.Stop(Self);
            return;
        }

        Persist(new PollingStopped(_timeProvider.GetUtcNow()), e =>
        {
            _state = _state.Apply(e);
            _log.Info("Polling stopped for {Target}", _state.Target);
            MaybeSnapshot();
            Context.Stop(Self);
        });
    }

    // Every poll ends in exactly one FetchReplied to Self: the gateway's answer, or a transport failure when no
    // gateway is registered or it does not answer within the reply timeout (which covers its queue, throttle
    // admission and HTTP timeout).
    private void Poll(string reason)
    {
        if (_state is not { Active: true, Target: { } target })
        {
            _log.Debug("Ignoring {Reason} poll - polling is not started", reason);
            return;
        }

        if (_inFlight is not null)
        {
            _log.Debug("Skipping {Reason} poll for {Location}/{Model} - a request is in flight",
                reason, target.Location, target.ModelId);
            return;
        }

        Timers.Cancel(PollTimerKey);
        var requestId = Guid.NewGuid();
        _inFlight = requestId;
        var location = target.Location;
        var modelId = target.ModelId;

        if (!Context.TryGetActor<IForecastGateway>(out var gateway))
        {
            Self.Tell(new FetchReplied(requestId,
                new FetchForecastFailed(location, modelId, PollFailureReason.Transport, "Gateway is not registered")));
            return;
        }

        var timeout = _fetchTimeouts.Reply;
        gateway.Ask<FetchForecastResponse>(new FetchForecast(location, modelId), timeout)
            .PipeTo(Self,
                success: reply => new FetchReplied(requestId, reply),
                failure: ex => new FetchReplied(requestId, new FetchForecastFailed(location, modelId, PollFailureReason.Transport,
                    ex is AskTimeoutException
                        ? $"Gateway did not answer within {timeout}"
                        : $"Gateway request failed ({ex.GetType().Name}: {ex.Message})")));
    }

    private void OnFetchReplied(FetchReplied msg)
    {
        if (msg.RequestId != _inFlight)
        {
            _log.Debug("Ignoring a reply to an earlier request: {Reply}", msg.Reply);
            return;
        }

        _inFlight = null;
        var (evt, outcome) = _state.ToEvent(msg.Reply, _timeProvider.GetUtcNow());
        Persist(evt, e =>
        {
            _state = _state.Apply(e);
            LogOutcome(msg.Reply, outcome);
            ScheduleNext();
            Report(outcome);
            MaybeSnapshot();
        });
    }

    private void LogOutcome(FetchForecastResponse reply, PollWorkerOutcome outcome)
    {
        if (_state.Target is not { } target)
        {
            return;
        }

        var (location, model) = (target.Location, target.ModelId);
        switch (reply)
        {
            case FetchForecastCompleted when outcome == PollWorkerOutcome.Changed:
                _log.Debug("Data changed for {Location}/{Model} - phase={Phase}, cycle={Cycle}",
                    location, model, _state.Phase, _state.Cycle);
                break;
            case FetchForecastCompleted:
                _log.Debug("Hash unchanged for {Location}/{Model} - miss={Miss}, phase={Phase}",
                    location, model, _state.MissCount, _state.Phase);
                break;
            case FetchForecastFailed { Reason: PollFailureReason.Transport } failed:
                _log.Warning("Fetch failed for {Location}/{Model} (transport) - tfc={Tfc}, details={Details}",
                    location, model, _state.TransientFailureCount, failed.Detail);
                break;
            case FetchForecastFailed { Reason: PollFailureReason.RateLimited }:
                _log.Warning("Fetch rate-limited for {Location}/{Model} - next poll at {Next}",
                    location, model, _state.NextPollUtc);
                break;
            case FetchForecastFailed failed:
                _log.Warning("Fetch failed for {Location}/{Model} ({Reason}: {Detail}) - retry scheduled",
                    location, model, failed.Reason, failed.Detail);
                break;
            case FetchForecastRejected rejected:
                _log.Warning("Fetch rejected for {Location}/{Model} ({Reason}) - next poll at {Next}",
                    location, model, rejected.Reason, _state.NextPollUtc);
                break;
        }
    }

    // The outcome of a request in flight reschedules, so nothing is planned while one is open.
    private void ScheduleNext()
    {
        if (_state is not { Active: true, Target: { } target } || _inFlight is not null)
        {
            return;
        }

        _log.Debug("Scheduling next poll for {Location}/{Model} at {NextPoll}", target.Location, target.ModelId, _state.NextPollUtc);
        var delay = _state.NextPollUtc - _timeProvider.GetUtcNow();
        if (delay <= TimeSpan.Zero)
        {
            Timers.Cancel(PollTimerKey);
            Self.Tell(PollDue.Instance);
        }
        else
        {
            Timers.StartSingleTimer(PollTimerKey, PollDue.Instance, delay);
        }
    }

    private void Report(PollWorkerOutcome outcome)
    {
        if (_state.Target is not { } target)
        {
            return;
        }

        if (Context.TryGetActor<IPollManager>(out var manager))
        {
            manager.Tell(new PollWorkerReport(target, outcome, _state.ToPollStateEntry()));
        }
        else
        {
            _log.Debug("Poll manager is not registered; {Outcome} report for {Target} not sent", outcome, target);
        }
    }

    private void MaybeSnapshot()
    {
        if (LastSequenceNr % SnapshotInterval == 0)
        {
            SaveSnapshot(_state.GetPersistenceState());
        }
    }
}
