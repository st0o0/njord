using Akka.Actor;
using Akka.Event;
using Akka.Persistence;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Persistence.Events.Polling;
using Servus.Akka;

namespace Njord.Pipeline;

// One persistent entity per location in the poll shard region. It owns the location's poll state,
// its timer and at most one fetch round in flight, and reports every start and outcome to the poll manager.
// On each tick it fetches all enabled domains (initially just Weather/Ensemble) in parallel.
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

    private readonly TimeProvider _timeProvider;
    private readonly FetchTimeouts _fetchTimeouts;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private PollWorkerState _state = PollWorkerState.Empty;
    private Guid? _roundId;
    private readonly Dictionary<DomainKind, FetchDomainResponse> _domainResults = new();
    private readonly HashSet<DomainKind> _pendingDomains = [];

    // Currently only Weather is enabled. More domains will be added as endpoints are implemented.
    internal static readonly DomainKind[] EnabledDomains = [DomainKind.Weather, DomainKind.AirQuality, DomainKind.Flood];

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
        Command<TriggerLocationPoll>(_ => Poll("trigger"));
        Command<PollDue>(_ => Poll("timer"));
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
        var evt = new PollingStarted(cmd.Target.Location.Name, cmd.DiscoveryInterval, _timeProvider.GetUtcNow());
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

    // Fetches all enabled domains in parallel for this location. Collects results with a timeout.
    private void Poll(string reason)
    {
        if (_state is not { Active: true, Target: { } target })
        {
            _log.Debug("Ignoring {Reason} poll - polling is not started", reason);
            return;
        }

        if (_roundId is not null)
        {
            _log.Debug("Skipping {Reason} poll for {Location} - a fetch round is in flight", reason, target.Location);
            return;
        }

        Timers.Cancel(PollTimerKey);
        var roundId = Guid.NewGuid();
        _roundId = roundId;
        _domainResults.Clear();
        _pendingDomains.Clear();

        if (!Context.TryGetActor<IForecastGateway>(out var gateway))
        {
            // No gateway: fail immediately
            OnRoundComplete(roundId);
            return;
        }

        var location = target.Location;
        var timeout = _fetchTimeouts.Reply;

        foreach (var domain in EnabledDomains)
        {
            _pendingDomains.Add(domain);
            var domainKind = domain;

            gateway.Ask<FetchDomainResponse>(new FetchDomain(location, domainKind), timeout)
                .PipeTo(Self,
                    success: reply => new DomainFetchReplied(roundId, domainKind, reply),
                    failure: ex => new DomainFetchReplied(roundId, domainKind,
                        new FetchDomainFailed(location, domainKind, PollFailureReason.Transport,
                            ex is AskTimeoutException
                                ? $"Gateway did not answer within {timeout}"
                                : $"Gateway request failed ({ex.GetType().Name}: {ex.Message})")));
        }

        // Safety timeout for the entire round
        Timers.StartSingleTimer(DomainTimeoutKey, new DomainFetchTimeout(roundId), timeout + TimeSpan.FromSeconds(5));
    }

    private void OnDomainFetchReplied(DomainFetchReplied msg)
    {
        if (msg.RequestId != _roundId)
        {
            _log.Debug("Ignoring a reply to an earlier fetch round: {Domain} {Reply}", msg.Domain, msg.Reply);
            return;
        }

        _domainResults[msg.Domain] = msg.Reply;
        _pendingDomains.Remove(msg.Domain);

        LogDomainOutcome(msg.Domain, msg.Reply);

        if (_pendingDomains.Count == 0)
        {
            Timers.Cancel(DomainTimeoutKey);
            OnRoundComplete(msg.RequestId);
        }
    }

    private void OnDomainFetchTimeout(DomainFetchTimeout msg)
    {
        if (msg.RoundId != _roundId)
        {
            return;
        }

        _log.Warning("Domain fetch round timed out with {Pending} pending domains", _pendingDomains.Count);
        foreach (var domain in _pendingDomains)
        {
            _domainResults[domain] = new FetchDomainFailed(
                _state.Target!.Location, domain, PollFailureReason.Transport, "Round timed out");
        }

        _pendingDomains.Clear();
        OnRoundComplete(msg.RoundId);
    }

    // Aggregate domain results into a single location-level outcome.
    private void OnRoundComplete(Guid roundId)
    {
        if (roundId != _roundId)
        {
            return;
        }

        _roundId = null;

        if (_domainResults.Count == 0)
        {
            // No gateway was registered - treat as a transport failure
            var now = _timeProvider.GetUtcNow();
            Persist(new PollFailed(now, TimeSpan.Zero), e =>
            {
                _state = _state.Apply(e);
                ScheduleNext();
                Report(PollWorkerOutcome.Failed);
                MaybeSnapshot();
            });
            return;
        }

        // Aggregate: any changed → Changed; all failed → Failed; otherwise Unchanged
        var anyChanged = _domainResults.Values.Any(r => r is FetchDomainCompleted c && c.Hash != _state.GetDomainHash(c.Domain));
        var allFailed = _domainResults.Values.All(r => r is not FetchDomainCompleted);

        // Use the first meaningful response to derive the location-level event.
        // Priority: rate limit > budget > pipeline unavailable > other failure > unchanged > changed
        var aggregateReply = ChooseAggregateReply();
        var (evt, outcome) = _state.ToEvent(aggregateReply, _timeProvider.GetUtcNow());

        Persist(evt, e =>
        {
            _state = _state.Apply(e);

            // Update per-domain hashes after persistence, so the next round's comparison is correct.
            foreach (var (domain, result) in _domainResults)
            {
                if (result is FetchDomainCompleted completed)
                {
                    _state = _state.WithDomainHash(domain, completed.Hash);
                }
            }

            ScheduleNext();
            Report(outcome);
            MaybeSnapshot();
        });
    }

    private FetchDomainResponse ChooseAggregateReply()
    {
        // If any domain changed, report changed (use the first changed result)
        var changed = _domainResults.Values.OfType<FetchDomainCompleted>()
            .FirstOrDefault(c => c.Hash != _state.GetDomainHash(c.Domain));
        if (changed is not null) return changed;

        // If any domain succeeded (unchanged), report unchanged
        var unchanged = _domainResults.Values.OfType<FetchDomainCompleted>().FirstOrDefault();
        if (unchanged is not null) return unchanged;

        // All failed: return the most significant failure
        var rateLimited = _domainResults.Values.OfType<FetchDomainFailed>()
            .FirstOrDefault(f => f.Reason == PollFailureReason.RateLimited);
        if (rateLimited is not null) return rateLimited;

        var budgetRejected = _domainResults.Values.OfType<FetchDomainRejected>()
            .FirstOrDefault(r => r.Reason == FetchRejectionReason.Budget);
        if (budgetRejected is not null) return budgetRejected;

        var pipelineUnavailable = _domainResults.Values.OfType<FetchDomainRejected>()
            .FirstOrDefault(r => r.Reason == FetchRejectionReason.PipelineUnavailable);
        if (pipelineUnavailable is not null) return pipelineUnavailable;

        // Any other failure
        return _domainResults.Values.First();
    }

    private void LogDomainOutcome(DomainKind domain, FetchDomainResponse reply)
    {
        if (_state.Target is not { } target)
        {
            return;
        }

        switch (reply)
        {
            case FetchDomainCompleted completed when completed.Hash != _state.GetDomainHash(domain):
                _log.Debug("Data changed for {Location}/{Domain} - phase={Phase}, cycle={Cycle}",
                    target.Location, domain, _state.Phase, _state.Cycle);
                break;
            case FetchDomainCompleted:
                _log.Debug("Hash unchanged for {Location}/{Domain} - miss={Miss}, phase={Phase}",
                    target.Location, domain, _state.MissCount, _state.Phase);
                break;
            case FetchDomainFailed { Reason: PollFailureReason.Transport } failed:
                _log.Warning("Fetch failed for {Location}/{Domain} (transport) - tfc={Tfc}, details={Details}",
                    target.Location, domain, _state.TransientFailureCount, failed.Detail);
                break;
            case FetchDomainFailed { Reason: PollFailureReason.RateLimited }:
                _log.Warning("Fetch rate-limited for {Location}/{Domain} - next poll at {Next}",
                    target.Location, domain, _state.NextPollUtc);
                break;
            case FetchDomainFailed failed:
                _log.Warning("Fetch failed for {Location}/{Domain} ({Reason}: {Detail}) - retry scheduled",
                    target.Location, domain, failed.Reason, failed.Detail);
                break;
            case FetchDomainRejected rejected:
                _log.Warning("Fetch rejected for {Location}/{Domain} ({Reason}) - next poll at {Next}",
                    target.Location, domain, rejected.Reason, _state.NextPollUtc);
                break;
        }
    }

    // The outcome of a request round reschedules, so nothing is planned while one is open.
    private void ScheduleNext()
    {
        if (_state is not { Active: true, Target: { } target } || _roundId is not null)
        {
            return;
        }

        _log.Debug("Scheduling next poll for {Location} at {NextPoll}", target.Location, _state.NextPollUtc);
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
