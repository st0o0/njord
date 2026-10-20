using System.Collections.Immutable;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence;
using Njord.Core;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Persistence.Events.Polling;
using Servus.Akka;

namespace Njord.Pipeline;

// One persistent entity per location in the poll shard region. It owns the location's poll state,
// its timer and at most one fetch round in flight, and reports every start and outcome to the poll manager.
// On each tick it fetches the enabled domains that are due, each on its own learned cycle, in parallel.
public sealed class PollWorker : ReceivePersistentActor, IWithTimers
{
    private const int SnapshotInterval = 50;
    private const string PollTimerKey = "poll";
    private const string DomainTimeoutKey = "domain-timeout";

    private sealed record PollDue
    {
        public static readonly PollDue Instance = new();
    }

    private sealed record DomainFetchReplied(Guid RequestId, DomainKind Domain, FetchDomainResponse Reply);
    private sealed record DomainFetchTimeout(Guid RoundId);

    private sealed record RoundState(
        Guid Id,
        ImmutableDictionary<DomainKind, FetchDomainResponse> Results,
        ImmutableHashSet<DomainKind> Pending);

    // In-flight round, not persisted; null while no request round is running.
    private RoundState? _round;

    private readonly TimeProvider _timeProvider;
    private readonly FetchTimeouts _fetchTimeouts;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly DomainKind[] _enabledDomains;
    private PollWorkerState _state = PollWorkerState.Empty;

    public ITimerScheduler Timers { get; set; } = null!;

    public override string PersistenceId { get; }

    public PollWorker(TimeProvider timeProvider, FetchTimeouts fetchTimeouts, IReadOnlySet<DomainKind> requiredDomains, string entityId)
    {
        _enabledDomains = [.. requiredDomains.Order()];
        _timeProvider = timeProvider;
        _fetchTimeouts = fetchTimeouts;
        PersistenceId = $"poll-worker-{entityId}";

        Recover<PollingStarted>(evt => _state = _state.Apply(evt));
        Recover<PollingStopped>(evt => _state = _state.Apply(evt));
        Recover<DomainPolled>(evt => _state = _state.Apply(evt));
        Recover<SnapshotOffer>(offer =>
            _state = offer.Snapshot is PersistedPollWorkerState snapshot ? snapshot.FromPersistence(_enabledDomains) : _state);
        Recover<RecoveryCompleted>(_ => OnRecovered());

        Command<StartPolling>(OnStartPolling);
        Command<StopPolling>(_ => OnStopPolling());
        Command<TriggerLocationPoll>(_ => Poll("trigger", force: true));
        Command<PollDue>(_ => Poll("timer", force: false));
        Command<DomainFetchReplied>(OnDomainFetchReplied);
        Command<DomainFetchTimeout>(OnDomainFetchTimeout);
        Command<SaveSnapshotSuccess>(success =>
        {
            _log.Debug("Snapshot saved at sequence {SequenceNr}, cleaning journal", success.Metadata.SequenceNr);
            DeleteMessages(success.Metadata.SequenceNr);
            DeleteSnapshots(new SnapshotSelectionCriteria(success.Metadata.SequenceNr - 1));
        });
        Command<SaveSnapshotFailure>(fail => _log.Warning(fail.Cause, "Snapshot save failed for {PersistenceId}", PersistenceId));
        Command<DeleteMessagesFailure>(fail => _log.Warning(fail.Cause, "Delete messages failed for {PersistenceId}", PersistenceId));
        Command<DeleteSnapshotsFailure>(fail => _log.Warning(fail.Cause, "Delete snapshots failed for {PersistenceId}", PersistenceId));
        Command<DeleteMessagesSuccess>(_ => { });
        Command<DeleteSnapshotsSuccess>(_ => { });
    }

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
        var evt = new PollingStarted(cmd.Target.Location.Name,
            [.. _enabledDomains.Select(d => (int)d)], cmd.DiscoveryInterval, _timeProvider.GetUtcNow());
        Persist(evt, e =>
        {
            _state = _state.Apply(e);
            if (wasStarted && previous != e.DiscoveryInterval)
            {
                _log.Debug("Discovery interval of {Location} changed from {Previous} to {Current}",
                    e.Location, previous, e.DiscoveryInterval);
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
            _log.Info("Polling stopped for {Location}", _state.Target);
            MaybeSnapshot();
            Context.Stop(Self);
        });
    }

    private void Poll(string reason, bool force)
    {
        if (_state is not { Active: true, Target: { } target })
        {
            _log.Debug("Ignoring {Reason} poll - polling is not started", reason);
            return;
        }

        if (_round is not null)
        {
            _log.Debug("Skipping {Reason} poll for {Location} - a fetch round is in flight", reason, target.Location);
            return;
        }

        Timers.Cancel(PollTimerKey);
        var roundId = Guid.NewGuid();
        IReadOnlyList<DomainKind> due = force ? _enabledDomains : _state.DueDomainsAt(_enabledDomains, _timeProvider.GetUtcNow());
        var domains = due.Count > 0 ? due : _enabledDomains;
        _round = new RoundState(roundId, ImmutableDictionary<DomainKind, FetchDomainResponse>.Empty, [.. domains]);

        if (!Context.TryGetActor<IForecastGateway>(out var gateway))
        {
            OnRoundComplete(roundId);
            return;
        }

        var location = target.Location;
        var timeout = _fetchTimeouts.Reply;
        foreach (var domain in domains)
        {
            gateway.AskDomain(location, domain, timeout).PipeTo(Self,
                success: reply => new DomainFetchReplied(roundId, domain, reply),
                failure: ex => new DomainFetchReplied(roundId, domain, PollRounds.AskFailure(location, domain, timeout, ex)));
        }

        Timers.StartSingleTimer(DomainTimeoutKey, new DomainFetchTimeout(roundId), timeout + TimeSpan.FromSeconds(5));
    }

    private void OnDomainFetchReplied(DomainFetchReplied msg)
    {
        if (_round is not { } round || msg.RequestId != round.Id)
        {
            _log.Debug("Ignoring a reply to an earlier fetch round: {Domain} {Reply}", msg.Domain, msg.Reply);
            return;
        }

        _round = round with { Results = round.Results.SetItem(msg.Domain, msg.Reply), Pending = round.Pending.Remove(msg.Domain) };
        _log.LogDomainOutcome(_state, msg.Domain, msg.Reply);

        if (_round.Pending.Count == 0)
        {
            Timers.Cancel(DomainTimeoutKey);
            OnRoundComplete(msg.RequestId);
        }
    }

    private void OnDomainFetchTimeout(DomainFetchTimeout msg)
    {
        if (_round is not { } round || msg.RoundId != round.Id)
        {
            return;
        }

        _log.Warning("Domain fetch round timed out with {Pending} pending domains", round.Pending.Count);
        _round = round with { Results = round.Results.TimeOut(round.Pending, _state.Target!.Location), Pending = [] };
        OnRoundComplete(msg.RoundId);
    }

    private void OnRoundComplete(Guid roundId)
    {
        if (_round is not { } round || roundId != round.Id)
        {
            return;
        }

        var results = round.Results;
        _round = null;
        var now = _timeProvider.GetUtcNow();

        if (results.IsEmpty)
        {
            // No gateway was registered — fail all enabled domains with a 1-minute retry
            var events = _enabledDomains.Select(d =>
                (object)new DomainPolled((int)d, 2, null, now, PollRounds.DefaultRetry)).ToList();
            PersistEvents(events, PollWorkerOutcome.Failed);
            return;
        }

        var domainEvents = PollRounds.ToDomainEvents(results, now, _state.GetDomainHash);
        var outcome = PollRounds.ChooseOutcome(domainEvents);
        PersistEvents(domainEvents, outcome);
    }

    private void PersistEvents(List<object> events, PollWorkerOutcome outcome)
    {
        var previousSequenceNr = LastSequenceNr;
        var remaining = events.Count;

        PersistAll(events, e =>
        {
            var before = e is DomainPolled { Outcome: 0 } polled ? _state.GetDomain((DomainKind)polled.Domain) : null;
            _state = _state.Apply(e);
            if (e is DomainPolled domainPolled)
            {
                _log.RecordDomainPoll(_state, domainPolled, before);
            }

            if (--remaining > 0)
            {
                return;
            }

            ScheduleNext();
            Report(outcome);
            if (LastSequenceNr / SnapshotInterval > previousSequenceNr / SnapshotInterval)
            {
                SaveSnapshot(_state.GetPersistenceState());
            }
        });
    }

    private void ScheduleNext()
    {
        if (_state is not { Active: true, Target: { } target } || _round is not null)
        {
            return;
        }

        var nextPoll = _state.NextPollUtc;
        var delay = nextPoll - _timeProvider.GetUtcNow();
        if (delay <= TimeSpan.Zero)
        {
            Timers.Cancel(PollTimerKey);
            Self.Tell(PollDue.Instance);
            return;
        }

        Timers.StartSingleTimer(PollTimerKey, PollDue.Instance, delay);
    }

    private void Report(PollWorkerOutcome outcome)
    {
        if (_state.Target is not { } target)
        {
            return;
        }

        if (!Context.TryGetActor<IPollManager>(out var manager))
        {
            _log.Debug("Poll manager is not registered; {Outcome} report for {Target} not sent", outcome, target);
            return;
        }

        manager.Tell(new PollWorkerReport(target, outcome, _state.ToPollStateEntry()));
    }

    private void MaybeSnapshot()
    {
        if (LastSequenceNr % SnapshotInterval == 0) SaveSnapshot(_state.GetPersistenceState());
    }
}
