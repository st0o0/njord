using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Messages.Polling;
using Terp.Akka;
using Terp.Resilience;

namespace Njord.Polling;

public sealed class PollManager : ReceiveActor, IWithTimers
{
    private sealed record RestartWorker(string LocationName);

    private static readonly BackoffPolicy _retryPolicy =
        Backoff.Create(TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromSeconds(30));

    private readonly TimeProvider _timeProvider;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IActorRef _pollRegion = Context.GetActor<IPollRegion>();
    private readonly PollManagerConfig _config;
    private PollManagerState _state;

    public ITimerScheduler Timers { get; set; } = null!;

    public PollManager(
        IOptions<NjordOptions> options,
        IOptions<PollingOptions> polling,
        TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _config = PollManagerConfig.Create(options.Value.ResolvedLocations);
        _state = PollManagerState.Create(_config, polling.Value.DiscoveryInterval, timeProvider.GetUtcNow());
        Receive<PollWorkerReport>(OnReport);
        Receive<Terminated>(OnWorkerTerminated);
        Receive<RestartWorker>(OnRestartWorker);
        Receive<QueryPollStates>(_ => Sender.Tell(_state.GetPollStates(_config)));
        Receive<QueryPollHealth>(_ => Sender.Tell(_state.GetPollHealth()));
        Receive<TriggerImmediatePoll>(OnTriggerImmediatePoll);
    }

    protected override void PreStart()
    {
        _log.Info("Starting {Count} poll workers (one per location)", _config.Targets.Count);
        foreach (var target in _config.Targets)
        {
            _pollRegion.Tell(new StartPolling(target, _state.DiscoveryInterval));
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

        var delay = _retryPolicy.DelayWithJitter(previousAttempts);
        _log.Warning("Poll worker {LocationName} terminated - starting it again in {Delay}", targetId, delay);
        Timers.StartSingleTimer($"restart-{targetId}", new RestartWorker(targetId), delay);
    }

    private void OnRestartWorker(RestartWorker msg)
    {
        if (_config.TargetsById.TryGetValue(msg.LocationName, out var target))
        {
            _pollRegion.Tell(new StartPolling(target, _state.DiscoveryInterval));
        }
    }

    private void OnTriggerImmediatePoll(TriggerImmediatePoll msg)
    {
        var targets = _config.Targets
            .Where(t => msg.Location is not { } location || t.Location.Name.Equals(location.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var target in targets)
        {
            _pollRegion.Tell(new TriggerLocationPoll(target.Location.Name));
        }

        _log.Debug("TriggerImmediatePoll: triggered {Count} polls", targets.Count);
        Sender.Tell(new TriggerImmediatePollCompleted(targets.Count, [.. targets.Select(t => t.ToString())]));
    }
}
