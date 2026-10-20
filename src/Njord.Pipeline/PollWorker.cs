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
    private static readonly TimeSpan DefaultRetry = TimeSpan.FromMinutes(1);

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
        Recover<RoundCompleted>(evt => _state = _state.Apply(evt));
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
            gateway.Ask<FetchDomainResponse>(new FetchDomain(location, domain), timeout).PipeTo(Self,
                success: reply => new DomainFetchReplied(roundId, domain, reply),
                failure: ex => new DomainFetchReplied(roundId, domain, AskFailure(location, domain, timeout, ex)));
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
        LogDomainOutcome(msg.Domain, msg.Reply);

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
        var timedOut = round.Pending.Aggregate(round.Results, (acc, domain) => acc.SetItem(domain,
            new FetchDomainFailed(_state.Target!.Location, domain, PollFailureReason.Transport, "Round timed out")));
        _round = round with { Results = timedOut, Pending = [] };
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

        ImmutableArray<DomainPollResult> domainResults;
        if (results.IsEmpty)
        {
            // No gateway was registered - fail all enabled domains with a default retry
            domainResults = [.. _enabledDomains.Select(d =>
                new DomainPollResult((int)d, PersistedDomainPollOutcome.Failed, null, DefaultRetry))];
        }
        else
        {
            domainResults = [.. results.OrderBy(kv => kv.Key).Select(kv => ToDomainResult(kv.Key, kv.Value))];
        }

        var evt = new RoundCompleted(domainResults, now);
        var outcome = ChooseOutcome(domainResults);
        var previousSequenceNr = LastSequenceNr;

        Persist(evt, e =>
        {
            foreach (var r in e.Results)
            {
                var domain = (DomainKind)r.Domain;
                var before = r.Outcome == PersistedDomainPollOutcome.Changed ? _state.GetDomain(domain) : null;
                _state = _state.ApplyDomainResult(r, e.Utc);
                RecordTelemetry(domain, r.Outcome, before);
            }

            ScheduleNext();
            Report(outcome);
            if (LastSequenceNr / SnapshotInterval > previousSequenceNr / SnapshotInterval)
            {
                SaveSnapshot(_state.GetPersistenceState());
            }
        });
    }

    private DomainPollResult ToDomainResult(DomainKind domain, FetchDomainResponse reply) => reply switch
    {
        FetchDomainCompleted c when c.Hash != _state.GetDomainHash(domain) =>
            new DomainPollResult((int)domain, PersistedDomainPollOutcome.Changed, c.Hash, null),
        FetchDomainCompleted =>
            new DomainPollResult((int)domain, PersistedDomainPollOutcome.Unchanged, null, null),
        _ =>
            new DomainPollResult((int)domain, PersistedDomainPollOutcome.Failed, null, FailureDelay(reply)),
    };

    private static TimeSpan FailureDelay(FetchDomainResponse reply) => reply switch
    {
        FetchDomainFailed { Reason: PollFailureReason.RateLimited } => PollWorkerState.RateLimitMinDelay,
        FetchDomainRejected { Reason: FetchRejectionReason.Budget } => PollWorkerState.RateLimitMinDelay,
        FetchDomainRejected { Reason: FetchRejectionReason.PipelineUnavailable } => PollWorkerState.PipelineUnavailableRetry,
        _ => DefaultRetry,
    };

    private static PollWorkerOutcome ChooseOutcome(ImmutableArray<DomainPollResult> results)
    {
        var hasChange = false;
        var hasSuccess = false;
        foreach (var r in results)
        {
            if (r.Outcome == PersistedDomainPollOutcome.Changed)
            {
                hasChange = true;
            }

            if (r.Outcome is PersistedDomainPollOutcome.Changed or PersistedDomainPollOutcome.Unchanged)
            {
                hasSuccess = true;
            }
        }

        return !hasSuccess ? PollWorkerOutcome.Failed
            : hasChange ? PollWorkerOutcome.Changed : PollWorkerOutcome.Unchanged;
    }

    private void ScheduleNext()
    {
        if (_state is not { Active: true, Target: { } } || _round is not null)
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

    private void LogDomainOutcome(DomainKind domain, FetchDomainResponse reply)
    {
        if (_state.Target is not { } target)
        {
            return;
        }

        var ds = _state.GetDomain(domain);
        switch (reply)
        {
            case FetchDomainCompleted completed when completed.Hash != ds.LastHash:
                _log.Debug("Data changed for {Location}/{Domain} - phase={Phase}, cycle={Cycle}",
                    target.Location, domain, ds.Phase, ds.LearnedCycle);
                break;
            case FetchDomainCompleted:
                _log.Debug("Hash unchanged for {Location}/{Domain} - miss={Miss}, phase={Phase}",
                    target.Location, domain, ds.MissCount, ds.Phase);
                break;
            case FetchDomainFailed { Reason: PollFailureReason.Transport } failed:
                _log.Warning("Fetch failed for {Location}/{Domain} (transport) - details={Details}",
                    target.Location, domain, failed.Detail);
                break;
            case FetchDomainFailed { Reason: PollFailureReason.RateLimited }:
                _log.Warning("Fetch rate-limited for {Location}/{Domain}",
                    target.Location, domain);
                break;
            case FetchDomainFailed failed:
                _log.Warning("Fetch failed for {Location}/{Domain} ({Reason}: {Detail}) - retry scheduled",
                    target.Location, domain, failed.Reason, failed.Detail);
                break;
            case FetchDomainRejected rejected:
                _log.Warning("Fetch rejected for {Location}/{Domain} ({Reason})",
                    target.Location, domain, rejected.Reason);
                break;
        }
    }

    private void RecordTelemetry(DomainKind domain, PersistedDomainPollOutcome outcome, DomainPollState? before)
    {
        Telemetry.RecordDomainPoll(domain, outcome);
        if (outcome == PersistedDomainPollOutcome.Changed && before is { Phase: PollPhase.Discovery } && _state.GetDomain(domain).Phase == PollPhase.Steady)
        {
            Telemetry.RecordCycleLearned(domain);
            _log.Info("Learned update cycle {Cycle} for {Location}/{Domain}",
                _state.GetDomain(domain).LearnedCycle, _state.Target?.Location, domain);
        }
    }

    private static FetchDomainFailed AskFailure(Location location, DomainKind domain, TimeSpan timeout, Exception ex) =>
        new(location, domain, PollFailureReason.Transport, ex is AskTimeoutException
            ? $"Gateway did not answer within {timeout}"
            : $"Gateway request failed ({ex.GetType().Name}: {ex.Message})");
}
