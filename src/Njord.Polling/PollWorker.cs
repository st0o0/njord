using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Event;
using Akka.Persistence;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Polling;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Polling;
using Servus.Akka;

namespace Njord.Polling;

// One persistent entity per location in the poll shard region. Each enabled domain has its own independent
// timer and fetch cycle. No rounds — a domain fires, fetches, persists its outcome and reschedules itself.
public sealed class PollWorker : ReceivePersistentActor, IWithTimers
{
    private const int SnapshotInterval = 50;

    private sealed record PollDomain(DomainKind Domain);
    private sealed record DomainFetchReplied(DomainKind Domain, FetchDomainResponse Reply);

    private readonly HashSet<DomainKind> _inFlight = [];

    private readonly TimeProvider _timeProvider;
    private readonly FetchTimeouts _fetchTimeouts;
    private readonly TimeSpan _pollInterval;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly DomainKind[] _enabledDomains;
    private PollWorkerState _state = PollWorkerState.Empty;

    public ITimerScheduler Timers { get; set; } = null!;

    public override string PersistenceId { get; }

    public PollWorker(TimeProvider timeProvider, FetchTimeouts fetchTimeouts, IOptions<PollingOptions> pollingOptions,
        IReadOnlySet<DomainKind> requiredDomains, string entityId)
    {
        _enabledDomains = [.. requiredDomains.Order()];
        _timeProvider = timeProvider;
        _fetchTimeouts = fetchTimeouts;
        _pollInterval = pollingOptions.Value.PollInterval;
        PersistenceId = $"poll-worker-{entityId}";

        Recover<PollingStarted>(evt => _state = _state.Apply(evt));
        Recover<PollingStopped>(evt => _state = _state.Apply(evt));
        Recover<DomainPolled>(evt => _state = _state.Apply(evt));
        Recover<SnapshotOffer>(offer =>
            _state = offer.Snapshot is PersistedPollWorkerState snapshot ? snapshot.FromPersistence(_enabledDomains) : _state);
        Recover<RecoveryCompleted>(_ => OnRecovered());

        Command<StartPolling>(OnStartPolling);
        Command<StopPolling>(_ => OnStopPolling());
        Command<TriggerLocationPoll>(_ => PollAll("trigger"));
        Command<PollDomain>(msg => PollSingle(msg.Domain, "timer"));
        Command<DomainFetchReplied>(OnDomainFetchReplied);
        Command<FetchDomainResponse>(reply => OnDomainFetchReplied(new DomainFetchReplied(reply.Domain, reply)));
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

        ScheduleAllDomains();
        Report(PollWorkerOutcome.Planned);
    }

    private void OnStartPolling(StartPolling cmd)
    {
        var location = cmd.Target.Location;
        if (_state is { Active: true, Target: { } current }
            && _state.DiscoveryInterval == cmd.DiscoveryInterval
            && current.Location.Latitude == location.Latitude
            && current.Location.Longitude == location.Longitude)
        {
            Report(PollWorkerOutcome.Planned);
            return;
        }

        var previous = _state.DiscoveryInterval;
        var wasStarted = _state.Target is not null;
        var evt = new PollingStarted(cmd.Target.Location.Name,
            [.. _enabledDomains.Select(d => (int)d)], cmd.DiscoveryInterval, _timeProvider.GetUtcNow(),
            location.Latitude, location.Longitude);
        Persist(evt, e =>
        {
            _state = _state.Apply(e);
            if (wasStarted && previous != e.DiscoveryInterval)
            {
                _log.Debug("Discovery interval of {Location} changed from {Previous} to {Current}",
                    e.Location, previous, e.DiscoveryInterval);
            }

            ScheduleAllDomains();
            Report(PollWorkerOutcome.Planned);
            MaybeSnapshot();
        });
    }

    private void OnStopPolling()
    {
        Timers.CancelAll();
        _inFlight.Clear();
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
            Context.Parent.Tell(new Passivate(PoisonPill.Instance));
        });
    }

    private void PollAll(string reason)
    {
        if (_state is not { Active: true, Target: not null })
        {
            _log.Debug("Ignoring {Reason} poll - polling is not started", reason);
            return;
        }

        foreach (var domain in _enabledDomains)
        {
            PollSingle(domain, reason);
        }
    }

    private void PollSingle(DomainKind domain, string reason)
    {
        if (_state is not { Active: true, Target: { } target })
        {
            _log.Debug("Ignoring {Reason} poll for {Domain} - polling is not started", reason, domain);
            return;
        }

        if (_inFlight.Contains(domain))
        {
            _log.Debug("Skipping {Reason} poll for {Location}/{Domain} - fetch is in flight", reason, target.Location, domain);
            return;
        }

        Timers.Cancel(TimerKey(domain));

        if (!Context.TryGetActor<IForecastGateway>(out var gateway))
        {
            OnDomainFetchResult(domain, null);
            return;
        }

        // The gateway answers through the poll shard region (routed to this entity by location name), not to the sender.
        _inFlight.Add(domain);
        var location = target.Location;
        gateway.Tell(new FetchDomain(location, domain));
        Timers.StartSingleTimer(ReplyTimerKey(domain),
            new DomainFetchReplied(domain, ReplyTimeout(location, domain, _fetchTimeouts.Reply)), _fetchTimeouts.Reply);
    }

    private void OnDomainFetchReplied(DomainFetchReplied msg)
    {
        if (!_inFlight.Remove(msg.Domain))
        {
            _log.Debug("Ignoring a reply for a domain not in flight: {Domain}", msg.Domain);
            return;
        }

        Timers.Cancel(ReplyTimerKey(msg.Domain));

        LogDomainOutcome(msg.Domain, msg.Reply);
        OnDomainFetchResult(msg.Domain, msg.Reply);
    }

    private void OnDomainFetchResult(DomainKind domain, FetchDomainResponse? reply)
    {
        var now = _timeProvider.GetUtcNow();
        var evt = ToDomainPolled(domain, reply, now);
        var before = evt.Outcome == PersistedDomainPollOutcome.Changed ? _state.GetDomain(domain) : null;
        var previousSequenceNr = LastSequenceNr;

        Persist(evt, e =>
        {
            _state = _state.Apply(e);
            RecordTelemetry(domain, e.Outcome, before);
            ScheduleDomain(domain);
            Report(ChooseOutcome(e.Outcome));
            if (LastSequenceNr / SnapshotInterval > previousSequenceNr / SnapshotInterval)
            {
                SaveSnapshot(_state.GetPersistenceState());
            }
        });
    }

    private DomainPolled ToDomainPolled(DomainKind domain, FetchDomainResponse? reply, DateTimeOffset now)
    {
        var failures = _state.GetDomain(domain).ConsecutiveFailures;
        return reply switch
        {
            FetchDomainCompleted c when c.Hash != _state.GetDomainHash(domain) =>
                new DomainPolled((int)domain, PersistedDomainPollOutcome.Changed, c.Hash, now, null),
            FetchDomainCompleted =>
                new DomainPolled((int)domain, PersistedDomainPollOutcome.Unchanged, null, now, null),
            FetchDomainRejected { Reason: FetchRejectionReason.PipelineUnavailable } =>
                new DomainPolled((int)domain, PersistedDomainPollOutcome.Failed, null, now, PollWorkerState.PipelineUnavailableRetry, failures),
            FetchDomainFailed { Reason: PollFailureReason.RateLimited } or FetchDomainRejected { Reason: FetchRejectionReason.Budget } =>
                new DomainPolled((int)domain, PersistedDomainPollOutcome.Failed, null, now,
                    FailureBackoff.RateLimited(failures + 1, _pollInterval), failures + 1),
            _ =>
                new DomainPolled((int)domain, PersistedDomainPollOutcome.Failed, null, now,
                    FailureBackoff.Delay(failures + 1, _pollInterval), failures + 1),
        };
    }

    private static PollWorkerOutcome ChooseOutcome(PersistedDomainPollOutcome outcome) => outcome switch
    {
        PersistedDomainPollOutcome.Changed => PollWorkerOutcome.Changed,
        PersistedDomainPollOutcome.Unchanged => PollWorkerOutcome.Unchanged,
        _ => PollWorkerOutcome.Failed,
    };

    private void ScheduleAllDomains()
    {
        foreach (var domain in _enabledDomains)
        {
            ScheduleDomain(domain);
        }
    }

    private void ScheduleDomain(DomainKind domain)
    {
        if (_state is not { Active: true, Target: { } } || _inFlight.Contains(domain))
        {
            return;
        }

        var ds = _state.GetDomain(domain);
        if (ds.NextPollAt is not { } nextPoll)
        {
            return;
        }

        var delay = nextPoll - _timeProvider.GetUtcNow();
        if (delay <= TimeSpan.Zero)
        {
            Timers.Cancel(TimerKey(domain));
            Self.Tell(new PollDomain(domain));
            return;
        }

        Timers.StartSingleTimer(TimerKey(domain), new PollDomain(domain), delay);
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
        if (LastSequenceNr % SnapshotInterval == 0)
        {
            SaveSnapshot(_state.GetPersistenceState());
        }
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

    private static string TimerKey(DomainKind domain) => $"poll-{domain}";

    private static string ReplyTimerKey(DomainKind domain) => $"reply-{domain}";

    private static FetchDomainFailed ReplyTimeout(Location location, DomainKind domain, TimeSpan timeout) =>
        new(location, domain, PollFailureReason.Transport, $"Gateway did not answer within {timeout}");
}
