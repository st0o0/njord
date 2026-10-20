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

    // In-flight round state — not persisted, lives only during a request round.
    private Guid _roundId;
    private ImmutableDictionary<DomainKind, FetchDomainResponse> _roundResults = ImmutableDictionary<DomainKind, FetchDomainResponse>.Empty;
    private ImmutableHashSet<DomainKind> _roundPending = [];
    private bool _roundActive;

    private readonly TimeProvider _timeProvider;
    private readonly FetchTimeouts _fetchTimeouts;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private PollWorkerState _state = PollWorkerState.Empty;

    private readonly DomainKind[] _enabledDomains;

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
        {
            if (offer.Snapshot is PersistedPollWorkerState snapshot)
            {
                _state = snapshot.FromPersistence(_enabledDomains);
            }
        });
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
        Command<SaveSnapshotFailure>(fail =>
            _log.Warning(fail.Cause, "Snapshot save failed for {PersistenceId}", PersistenceId));
        Command<DeleteMessagesFailure>(fail =>
            _log.Warning(fail.Cause, "Delete messages failed for {PersistenceId}", PersistenceId));
        Command<DeleteSnapshotsFailure>(fail =>
            _log.Warning(fail.Cause, "Delete snapshots failed for {PersistenceId}", PersistenceId));
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

        if (_roundActive)
        {
            _log.Debug("Skipping {Reason} poll for {Location} - a fetch round is in flight", reason, target.Location);
            return;
        }

        Timers.Cancel(PollTimerKey);
        _roundId = Guid.NewGuid();
        IReadOnlyList<DomainKind> due = force ? _enabledDomains : _state.DueDomainsAt(_enabledDomains, _timeProvider.GetUtcNow());
        var domains = due.Count > 0 ? due : _enabledDomains;
        _roundPending = [.. domains];
        _roundResults = ImmutableDictionary<DomainKind, FetchDomainResponse>.Empty;
        _roundActive = true;

        if (!Context.TryGetActor<IForecastGateway>(out var gateway))
        {
            OnRoundComplete(_roundId);
            return;
        }

        var location = target.Location;
        var timeout = _fetchTimeouts.Reply;

        foreach (var domain in domains)
        {
            var domainKind = domain;
            var roundId = _roundId;

            gateway.Ask<FetchDomainResponse>(new FetchDomain(location, domainKind), timeout)
                .PipeTo(Self,
                    success: reply => new DomainFetchReplied(roundId, domainKind, reply),
                    failure: ex => new DomainFetchReplied(roundId, domainKind,
                        new FetchDomainFailed(location, domainKind, PollFailureReason.Transport,
                            ex is AskTimeoutException
                                ? $"Gateway did not answer within {timeout}"
                                : $"Gateway request failed ({ex.GetType().Name}: {ex.Message})")));
        }

        Timers.StartSingleTimer(DomainTimeoutKey, new DomainFetchTimeout(_roundId), timeout + TimeSpan.FromSeconds(5));
    }

    private void OnDomainFetchReplied(DomainFetchReplied msg)
    {
        if (!_roundActive || msg.RequestId != _roundId)
        {
            _log.Debug("Ignoring a reply to an earlier fetch round: {Domain} {Reply}", msg.Domain, msg.Reply);
            return;
        }

        _roundResults = _roundResults.SetItem(msg.Domain, msg.Reply);
        _roundPending = _roundPending.Remove(msg.Domain);

        LogDomainOutcome(msg.Domain, msg.Reply);

        if (_roundPending.Count == 0)
        {
            Timers.Cancel(DomainTimeoutKey);
            OnRoundComplete(msg.RequestId);
        }
    }

    private void OnDomainFetchTimeout(DomainFetchTimeout msg)
    {
        if (!_roundActive || msg.RoundId != _roundId)
        {
            return;
        }

        var location = _state.Target!.Location;
        _log.Warning("Domain fetch round timed out with {Pending} pending domains", _roundPending.Count);
        foreach (var domain in _roundPending)
        {
            _roundResults = _roundResults.SetItem(domain,
                new FetchDomainFailed(location, domain, PollFailureReason.Transport, "Round timed out"));
        }

        _roundPending = [];
        OnRoundComplete(msg.RoundId);
    }

    private void OnRoundComplete(Guid roundId)
    {
        if (!_roundActive || roundId != _roundId)
        {
            return;
        }

        var results = _roundResults;
        _roundActive = false;
        _roundResults = ImmutableDictionary<DomainKind, FetchDomainResponse>.Empty;
        _roundPending = [];

        var now = _timeProvider.GetUtcNow();

        if (results.IsEmpty)
        {
            // No gateway was registered — fail all enabled domains with a 1-minute retry
            var events = _enabledDomains.Select(d =>
                (object)new DomainPolled((int)d, 2, null, now, TimeSpan.FromMinutes(1))).ToList();
            PersistEvents(events, PollWorkerOutcome.Failed);
            return;
        }

        var domainEvents = ToDomainEvents(results, now);
        var outcome = ChooseOutcome(domainEvents);
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
            RecordDomainTelemetry(e, before);

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

    private List<object> ToDomainEvents(
        IReadOnlyDictionary<DomainKind, FetchDomainResponse> results, DateTimeOffset now)
    {
        var events = new List<object>(results.Count);
        foreach (var (domain, reply) in results.OrderBy(kv => kv.Key))
        {
            events.Add(reply switch
            {
                FetchDomainCompleted c when c.Hash != _state.GetDomainHash(domain) =>
                    new DomainPolled((int)domain, 0, c.Hash, now, null),
                FetchDomainCompleted =>
                    new DomainPolled((int)domain, 1, null, now, null),
                _ =>
                    new DomainPolled((int)domain, 2, null, now, FailureDelay(reply, results)),
            });
        }

        return events;
    }

    private static TimeSpan FailureDelay(FetchDomainResponse reply,
        IReadOnlyDictionary<DomainKind, FetchDomainResponse> results)
    {
        var anyCompleted = results.Values.Any(r => r is FetchDomainCompleted);
        if (!anyCompleted)
        {
            // All-failed round: use the aggregate-level delay
            return reply switch
            {
                FetchDomainFailed { Reason: PollFailureReason.RateLimited } => PollWorkerState.RateLimitMinDelay,
                FetchDomainRejected { Reason: FetchRejectionReason.Budget } => PollWorkerState.RateLimitMinDelay,
                FetchDomainRejected { Reason: FetchRejectionReason.PipelineUnavailable } => PollWorkerState.PipelineUnavailableRetry,
                _ => TimeSpan.FromMinutes(1),
            };
        }

        return reply switch
        {
            FetchDomainFailed { Reason: PollFailureReason.RateLimited } => PollWorkerState.RateLimitMinDelay,
            FetchDomainRejected { Reason: FetchRejectionReason.Budget } => PollWorkerState.RateLimitMinDelay,
            FetchDomainRejected { Reason: FetchRejectionReason.PipelineUnavailable } => PollWorkerState.PipelineUnavailableRetry,
            _ => TimeSpan.FromMinutes(1),
        };
    }

    private static PollWorkerOutcome ChooseOutcome(List<object> domainEvents)
    {
        var hasChanged = false;
        var hasCompleted = false;
        foreach (var evt in domainEvents)
        {
            if (evt is DomainPolled polled)
            {
                switch (polled.Outcome)
                {
                    case 0: hasChanged = true; hasCompleted = true; break;
                    case 1: hasCompleted = true; break;
                }
            }
        }

        if (!hasCompleted)
        {
            return PollWorkerOutcome.Failed;
        }

        return hasChanged ? PollWorkerOutcome.Changed : PollWorkerOutcome.Unchanged;
    }

    private void RecordDomainTelemetry(object evt, DomainPollState? before)
    {
        if (evt is not DomainPolled polled)
        {
            return;
        }

        var domain = (DomainKind)polled.Domain;
        var outcomeName = polled.Outcome switch { 0 => "changed", 1 => "unchanged", _ => "failed" };
        Telemetry.DomainPolls.Add(1, DomainTag(domain), new KeyValuePair<string, object?>("outcome", outcomeName));

        if (polled.Outcome == 0 && before is { Phase: PollPhase.Discovery } && _state.GetDomain(domain).Phase == PollPhase.Steady)
        {
            Telemetry.CycleLearned.Add(1, DomainTag(domain));
            _log.Info("Learned update cycle {Cycle} for {Location}/{Domain}",
                _state.GetDomain(domain).LearnedCycle, _state.Target?.Location, domain);
        }
    }

    private static KeyValuePair<string, object?> DomainTag(DomainKind domain) => new("domain", domain switch
    {
        DomainKind.Weather => "weather",
        DomainKind.AirQuality => "air_quality",
        DomainKind.Flood => "flood",
        DomainKind.Solar => "solar",
        DomainKind.Marine => "marine",
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unknown domain kind"),
    });

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

    private void ScheduleNext()
    {
        if (_state is not { Active: true, Target: { } target } || _roundActive)
        {
            return;
        }

        var nextPoll = _state.NextPollUtc;
        _log.Debug("Scheduling next poll for {Location} at {NextPoll}", target.Location, nextPoll);
        var delay = nextPoll - _timeProvider.GetUtcNow();
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
