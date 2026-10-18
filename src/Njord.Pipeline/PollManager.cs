using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Event;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Messages.Pipeline;
using Njord.Pipeline.Health;
using Njord.Model;
using Servus.Akka;
using Servus.Resilience;

namespace Njord.Pipeline;

// Cluster singleton that orchestrates one poll worker per configured target in the poll shard region: it starts,
// re-plans, stops and restarts them, forwards triggers, and keeps the views built from their reports (poll states,
// poll-cycle summaries and metrics, poll health). It persists nothing; the workers own the poll state.
public sealed class PollManager : ReceiveActor, IWithTimers
{
    private const string ResolveTimerKey = "resolve-region";

    private sealed record ResolveRegion;
    private sealed record RegionStateReceived(CurrentShardRegionState State);
    private sealed record RegionStateFailed(Exception Cause);
    private sealed record RestartWorker(string TargetId);

    private static readonly BackoffPolicy RetryPolicy =
        Backoff.Create(TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromSeconds(30));

    private static readonly TimeSpan RegionStateTimeout = TimeSpan.FromSeconds(10);

    private readonly TimeProvider _timeProvider;
    private readonly PollHealthState _health;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly List<PollTarget> _targets = [];
    private readonly Dictionary<string, PollTarget> _targetsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _modelsByLocation = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PollStateEntry> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<IActorRef, string> _watched = [];
    private readonly Dictionary<string, int> _restartAttempts = new(StringComparer.Ordinal);
    private readonly PollCycleTracker _cycles = new();
    private TimeSpan _discoveryInterval;
    private IActorRef? _region;
    private int _resolveAttempts;

    public ITimerScheduler Timers { get; set; } = null!;

    public PollManager(
        IOptions<NjordOptions> options,
        ConfigView config,
        TimeProvider timeProvider,
        PollHealthState health)
    {
        _timeProvider = timeProvider;
        _health = health;
        _discoveryInterval = config.Polling.DiscoveryInterval;

        var now = timeProvider.GetUtcNow();
        foreach (var location in options.Value.Locations)
        {
            var models = options.Value.Models.Union(location.Models ?? [], StringComparer.OrdinalIgnoreCase).ToList();
            _modelsByLocation[location.Name] = models;
            foreach (var model in models)
            {
                var target = new PollTarget(new LocationId(location.Name), model);
                _targets.Add(target);
                _targetsById[target.Id] = target;
                _states[target.Id] = new PollStateEntry(target.Location, model, PollPhase.Discovery, now, null, 0, null);
            }
        }

        Receive<ResolveRegion>(_ => OnResolveRegion());
        Receive<RegionStateReceived>(OnRegionState);
        Receive<RegionStateFailed>(msg =>
            _log.Warning(msg.Cause, "Poll region did not report its workers; removed targets are not stopped"));
        Receive<PollWorkerReport>(OnReport);
        Receive<Terminated>(OnWorkerTerminated);
        Receive<RestartWorker>(OnRestartWorker);
        Receive<QueryPollStates>(_ => Sender.Tell(new PollStatesResult([.. _targets.Select(t => _states[t.Id])])));
        Receive<TriggerImmediatePoll>(OnTriggerImmediatePoll);
        Receive<PollingChanged>(OnPollingChanged);
    }

    protected override void PreStart() => Self.Tell(new ResolveRegion());

    private void OnResolveRegion()
    {
        if (!Context.TryGetActor<IPollRegion>(out var region))
        {
            var delay = RetryPolicy.DelayWithJitter(_resolveAttempts++);
            _log.Warning("Poll region is not registered - retrying in {Delay}", delay);
            Timers.StartSingleTimer(ResolveTimerKey, new ResolveRegion(), delay);
            return;
        }

        _region = region;
        _log.Info("Starting {Count} poll workers", _targets.Count);
        foreach (var target in _targets)
        {
            region.Tell(new StartPolling(target, _discoveryInterval));
        }

        region.Ask<CurrentShardRegionState>(GetShardRegionState.Instance, RegionStateTimeout)
            .PipeTo(Self, success: state => new RegionStateReceived(state), failure: ex => new RegionStateFailed(ex));
    }

    // Workers of targets that are no longer configured are stopped; their journals stay.
    private void OnRegionState(RegionStateReceived msg)
    {
        var removed = msg.State.Shards
            .SelectMany(shard => shard.EntityIds)
            .Where(id => !_targetsById.ContainsKey(id))
            .Distinct(StringComparer.Ordinal);
        foreach (var id in removed)
        {
            _log.Info("Stopping the poll worker {TargetId} - its target is no longer configured", id);
            _region?.Tell(new StopPolling(id));
        }
    }

    private void OnReport(PollWorkerReport report)
    {
        if (!_targetsById.TryGetValue(report.Target.Id, out var target))
        {
            _log.Warning("Ignoring a poll report for unknown target {Target}", report.Target);
            return;
        }

        _states[target.Id] = report.State;
        if (!_watched.ContainsKey(Sender) && !Sender.IsNobody())
        {
            Context.Watch(Sender);
            _watched[Sender] = target.Id;
        }

        if (report.Outcome == PollWorkerOutcome.Planned)
        {
            return;
        }

        _restartAttempts.Remove(target.Id);
        var now = _timeProvider.GetUtcNow();
        switch (report.Outcome)
        {
            case PollWorkerOutcome.Changed:
                _health.RecordSuccessfulPoll(now);
                Telemetry.DataChanged.Add(1,
                    new KeyValuePair<string, object?>("location", target.Location.Name),
                    new KeyValuePair<string, object?>("model", target.ModelId));
                RecordCycle(target, PollCycleOutcome.Changed, now);
                break;
            case PollWorkerOutcome.Unchanged:
                _health.RecordSuccessfulPoll(now);
                RecordCycle(target, PollCycleOutcome.Unchanged, now);
                break;
            case PollWorkerOutcome.Failed:
                RecordCycle(target, PollCycleOutcome.Failed, now);
                break;
        }
    }

    private void RecordCycle(PollTarget target, PollCycleOutcome outcome, DateTimeOffset now)
    {
        var resolved = _modelsByLocation[target.Location.Name];
        if (_cycles.Record(target.Location.Name, target.ModelId, resolved, outcome, now) is not { } summary)
        {
            return;
        }

        var locationTag = new KeyValuePair<string, object?>("location", summary.Location);
        Telemetry.PollCycleDuration.Record(summary.Duration.TotalSeconds, locationTag);
        Telemetry.PollCycleModels.Record(summary.Total, locationTag);
        Telemetry.PollCycleFailedModels.Record(summary.Failed, locationTag);
        _log.Debug(
            "Poll complete for {Location}: {Changed}/{Total} models changed, {Failed} failed in {Duration}ms",
            summary.Location, summary.Changed, summary.Total, summary.Failed, summary.Duration.TotalMilliseconds);
    }

    // A worker that stopped unexpectedly (e.g. on a journal failure) is started again; it recovers its state.
    private void OnWorkerTerminated(Terminated msg)
    {
        if (!_watched.Remove(msg.ActorRef, out var targetId) || !_targetsById.ContainsKey(targetId))
        {
            return;
        }

        // The shard region hands off (stops) every worker when the node shuts down.
        if (CoordinatedShutdown.Get(Context.System).ShutdownReason is not null)
        {
            _log.Debug("Poll worker {TargetId} stopped during shutdown", targetId);
            return;
        }

        var attempts = _restartAttempts.GetValueOrDefault(targetId);
        _restartAttempts[targetId] = attempts + 1;
        var delay = RetryPolicy.DelayWithJitter(attempts);
        _log.Warning("Poll worker {TargetId} terminated - starting it again in {Delay}", targetId, delay);
        Timers.StartSingleTimer($"restart-{targetId}", new RestartWorker(targetId), delay);
    }

    private void OnRestartWorker(RestartWorker msg)
    {
        if (_region is not null && _targetsById.TryGetValue(msg.TargetId, out var target))
        {
            _region.Tell(new StartPolling(target, _discoveryInterval));
        }
    }

    private void OnTriggerImmediatePoll(TriggerImmediatePoll msg)
    {
        if (_region is null)
        {
            Sender.Tell(new TriggerImmediatePollFailed(new InvalidOperationException("The poll region is not available yet")));
            return;
        }

        var targets = _targets
            .Where(t => msg.Location is not { } location || t.Location.Name.Equals(location.Name, StringComparison.OrdinalIgnoreCase))
            .Where(t => string.IsNullOrEmpty(msg.Model) || t.ModelId.Equals(msg.Model, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var target in targets)
        {
            _region.Tell(new TriggerTargetPoll(target.Id));
        }

        _log.Debug("TriggerImmediatePoll: triggered {Count} polls", targets.Count);
        Sender.Tell(new TriggerImmediatePollCompleted(targets.Count, [.. targets.Select(t => t.ToString())]));
    }

    private void OnPollingChanged(PollingChanged msg)
    {
        var previous = _discoveryInterval;
        if (previous == msg.DiscoveryInterval)
        {
            return;
        }

        _discoveryInterval = msg.DiscoveryInterval;
        if (_region is not null)
        {
            foreach (var target in _targets)
            {
                _region.Tell(new StartPolling(target, _discoveryInterval));
            }
        }

        _log.Info("Discovery interval changed from {Previous} to {Current}", previous, msg.DiscoveryInterval);
    }
}
