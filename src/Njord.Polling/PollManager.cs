using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Event;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Messages.Polling;
using Servus.Akka;
using Servus.Resilience;

namespace Njord.Polling;

// Cluster singleton that orchestrates one poll worker per configured location in the poll shard region: it starts,
// re-plans, stops and restarts them, forwards triggers, and keeps the views built from their reports (poll states,
// poll health). It persists nothing; the workers own the poll state.
public sealed class PollManager : ReceiveActor, IWithTimers
{
    private const string ResolveTimerKey = "resolve-region";

    private sealed record ResolveRegion;
    private sealed record RegionStateReceived(CurrentShardRegionState State);
    private sealed record RegionStateFailed(Exception Cause);
    private sealed record RestartWorker(string LocationName);

    private static readonly BackoffPolicy RetryPolicy =
        Backoff.Create(TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromSeconds(30));

    private static readonly TimeSpan RegionStateTimeout = TimeSpan.FromSeconds(10);

    private readonly TimeProvider _timeProvider;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly PollManagerConfig _config;
    private PollManagerState _state;

    public ITimerScheduler Timers { get; set; } = null!;

    public PollManager(
        IOptions<NjordOptions> options,
        IOptions<PollingOptions> polling,
        TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _config = PollManagerConfig.Create(options.Value.Locations);
        _state = PollManagerState.Create(_config, polling.Value.DiscoveryInterval, timeProvider.GetUtcNow());

        Receive<ResolveRegion>(_ => OnResolveRegion());
        Receive<RegionStateReceived>(OnRegionState);
        Receive<RegionStateFailed>(msg =>
            _log.Warning(msg.Cause, "Poll region did not report its workers; removed targets are not stopped"));
        Receive<PollWorkerReport>(OnReport);
        Receive<Terminated>(OnWorkerTerminated);
        Receive<RestartWorker>(OnRestartWorker);
        Receive<QueryPollStates>(_ => Sender.Tell(_state.GetPollStates(_config)));
        Receive<QueryPollHealth>(_ => Sender.Tell(_state.GetPollHealth()));
        Receive<TriggerImmediatePoll>(OnTriggerImmediatePoll);
    }

    protected override void PreStart() => Self.Tell(new ResolveRegion());

    private void OnResolveRegion()
    {
        if (!Context.TryGetActor<IPollRegion>(out var region))
        {
            _state = _state.ApplyResolveRetry();
            var delay = RetryPolicy.DelayWithJitter(_state.ResolveAttempts);
            _log.Warning("Poll region is not registered - retrying in {Delay}", delay);
            Timers.StartSingleTimer(ResolveTimerKey, new ResolveRegion(), delay);
            return;
        }

        _state = _state.ApplyRegionResolved(region);
        _log.Info("Starting {Count} poll workers (one per location)", _config.Targets.Count);
        foreach (var target in _config.Targets)
        {
            region.Tell(new StartPolling(target, _state.DiscoveryInterval));
        }

        region.Ask<CurrentShardRegionState>(GetShardRegionState.Instance, RegionStateTimeout)
            .PipeTo(Self, success: state => new RegionStateReceived(state), failure: ex => new RegionStateFailed(ex));
    }

    private void OnRegionState(RegionStateReceived msg)
    {
        var removed = msg.State.Shards
            .SelectMany(shard => shard.EntityIds)
            .Where(id => !_config.TargetsById.ContainsKey(id))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var id in removed)
        {
            _log.Info("Stopping the poll worker {LocationName} - its location is no longer configured", id);
            _state.Region?.Tell(new StopPolling(id));
        }
    }

    private void OnReport(PollWorkerReport report)
    {
        var previous = _state;
        _state = _state.ApplyReport(report, Sender, _config, _timeProvider.GetUtcNow());

        if (!previous.Watched.ContainsKey(Sender) && _state.Watched.ContainsKey(Sender))
        {
            Context.Watch(Sender);
        }

        if (report.Outcome == PollWorkerOutcome.Changed && _config.TargetsById.ContainsKey(report.Target.Id))
        {
            Telemetry.DataChanged.Add(1,
                new KeyValuePair<string, object?>("location", report.Target.Location.Name));
        }
    }

    private void OnWorkerTerminated(Terminated msg)
    {
        _state = _state.ApplyWorkerTerminated(msg.ActorRef, out var targetId, out var previousAttempts);
        if (targetId is null || !_config.TargetsById.ContainsKey(targetId))
        {
            return;
        }

        if (CoordinatedShutdown.Get(Context.System).ShutdownReason is not null)
        {
            _log.Debug("Poll worker {LocationName} stopped during shutdown", targetId);
            return;
        }

        var delay = RetryPolicy.DelayWithJitter(previousAttempts);
        _log.Warning("Poll worker {LocationName} terminated - starting it again in {Delay}", targetId, delay);
        Timers.StartSingleTimer($"restart-{targetId}", new RestartWorker(targetId), delay);
    }

    private void OnRestartWorker(RestartWorker msg)
    {
        if (_state.Region is not null && _config.TargetsById.TryGetValue(msg.LocationName, out var target))
        {
            _state.Region.Tell(new StartPolling(target, _state.DiscoveryInterval));
        }
    }

    private void OnTriggerImmediatePoll(TriggerImmediatePoll msg)
    {
        if (_state.Region is null)
        {
            Sender.Tell(new TriggerImmediatePollFailed(new InvalidOperationException("The poll region is not available yet")));
            return;
        }

        var targets = _config.Targets
            .Where(t => msg.Location is not { } location || t.Location.Name.Equals(location.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var target in targets)
        {
            _state.Region.Tell(new TriggerLocationPoll(target.Location.Name));
        }

        _log.Debug("TriggerImmediatePoll: triggered {Count} polls", targets.Count);
        Sender.Tell(new TriggerImmediatePollCompleted(targets.Count, [.. targets.Select(t => t.ToString())]));
    }
}
