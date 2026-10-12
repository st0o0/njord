using Akka.Actor;
using Akka.Event;
using Akka.Persistence;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Health;
using Njord.Messages.Common;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using PersistedDataChanged = Njord.Persistence.Events.Pipeline.DataChanged;
using Njord.Weather;
using Servus.Akka;
using Servus.Resilience;

namespace Njord.Pipeline;

public sealed class PollScheduler : ReceivePersistentActor
{

    public override string PersistenceId => "scheduler";

    private readonly NjordOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly NjordHealthState _healthState;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private PollSchedulerState _state = PollSchedulerState.Empty;
    private readonly Dictionary<string, PollCycleTracker> _pollCycles = new();

    private IActorRef? _gateway;
    private IActorRef? _lastTerminatedGateway;
    private int _gatewayRetryCount;

    private sealed record GatewayResolved(IActorRef Gateway);
    private sealed record GatewayResolveFailed(Exception Cause);
    private sealed record RetryGatewayResolve;
    private sealed record PollCycleTracker(DateTimeOffset Start, int Changed, int Reported);

    private static readonly BackoffPolicy GatewayRetryPolicy =
        Backoff.Create(TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromSeconds(30));

    private static readonly TimeSpan RateLimitMinDelay = TimeSpan.FromMinutes(5);

    public PollScheduler(
        IOptions<NjordOptions> options,
        TimeProvider timeProvider,
        NjordHealthState healthState)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _healthState = healthState;

        _gateway = Context.GetActor<IForecastGateway>();
        Context.Watch(_gateway);

        Recover<PersistedDataChanged>(dto => _state = _state.ApplyRecover(dto, _options.DiscoveryInterval));
        Recover<SnapshotOffer>(_ => { });

        Ready();
    }

    protected override void PreStart()
    {
        InitializeStates();
        _log.Info("Gateway resolved - scheduling initial polls");
    }

    private void WaitingForGateway()
    {
        Command<GatewayResolved>(msg =>
        {
            if (Equals(msg.Gateway, _lastTerminatedGateway))
            {
                var delay = GatewayRetryPolicy.DelayWithJitter(_gatewayRetryCount);
                _gatewayRetryCount++;
                Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, new RetryGatewayResolve(), Self);
                return;
            }

            _lastTerminatedGateway = null;
            _gateway = msg.Gateway;
            Context.Watch(_gateway);
            _gatewayRetryCount = 0;

            InitializeStates();
            _log.Info("Gateway resolved - scheduling initial polls");
            Become(Ready);
            Stash.UnstashAll();
        });
        Command<RetryGatewayResolve>(_ =>
        {
            Context.GetActorAsync<IForecastGateway>()
                .PipeTo(Self, success: r => new GatewayResolved(r), failure: ex => new GatewayResolveFailed(ex));
        });
        Command<GatewayResolveFailed>(msg =>
        {
            _log.Warning(msg.Cause, "Failed to resolve Gateway - retrying");
            var delay = GatewayRetryPolicy.DelayWithJitter(_gatewayRetryCount);
            _gatewayRetryCount++;
            Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, new RetryGatewayResolve(), Self);
        });
        Command<QueryPollStates>(OnQueryPollStates);
        CommandAny(_ => Stash.Stash());
    }

    private void Ready()
    {
        Command<ScheduledPoll>(OnScheduledPoll);
        Command<FetchForecastCompleted>(OnFetchForecastCompleted);
        Command<FetchForecastFailed>(OnFetchForecastFailed);
        Command<TriggerImmediatePoll>(OnTriggerImmediatePoll);
        Command<QueryPollStates>(OnQueryPollStates);
        Command<Terminated>(OnTerminated);
    }

    private void OnTerminated(Terminated msg)
    {
        _log.Warning("Gateway terminated - waiting for new ref");
        _lastTerminatedGateway = msg.ActorRef;
        _gateway = null;
        _gatewayRetryCount = 0;

        Context.GetActorAsync<IForecastGateway>()
            .PipeTo(Self, success: r => new GatewayResolved(r), failure: ex => new GatewayResolveFailed(ex));

        Become(WaitingForGateway);
    }

    private void InitializeStates()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var location in _options.Locations)
        {
            foreach (var modelId in _options.Models.Union(location.Models ?? [], StringComparer.OrdinalIgnoreCase))
            {
                var key = Key(location.Name, modelId);
                _state = _state.EnsureInitialized(key, now);
                ScheduleNext(location.Name, modelId);
            }
        }
    }

    private void OnScheduledPoll(ScheduledPoll poll)
    {
        if (_gateway is null)
            return;

        _gateway.Tell(new FetchForecast(poll.Location, poll.ModelId));
    }

    private void OnTriggerImmediatePoll(TriggerImmediatePoll msg)
    {
        var targets = new List<string>();
        IEnumerable<LocationOptions> locations = string.IsNullOrEmpty(msg.Location)
            ? _options.Locations
            : _options.FindLocation(msg.Location) is { } found ? [found] : [];

        foreach (var location in locations)
        {
            var resolved = _options.Models.Union(location.Models ?? [], StringComparer.OrdinalIgnoreCase).ToList();
            var models = string.IsNullOrEmpty(msg.Model)
                ? resolved
                : resolved
                    .Where(m => m.Equals(msg.Model, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            foreach (var modelId in models)
            {
                Self.Tell(new ScheduledPoll(location.Name, modelId));
                targets.Add($"{location.Name}/{modelId}");
            }
        }

        _log.Debug("TriggerImmediatePoll: triggered {Count} polls", targets.Count);
        Sender.Tell(new TriggerImmediatePollCompleted(targets.Count, targets));
    }

    private void OnFetchForecastCompleted(FetchForecastCompleted result)
    {
        var key = Key(result.Location, result.ModelId);
        var now = _timeProvider.GetUtcNow();
        var (updatedState, decision) = _state.ApplyHashResult(key, result.Hash, now, _options.DiscoveryInterval);

        if (decision == HashDecision.Changed)
        {
            var evt = new DataChanged(result.Location, result.ModelId, result.Hash, now);
            var dto = PollSchedulerStateExtensions.ToPersistedEvent(evt);
            Persist(dto, _ =>
            {
                _state = updatedState;
                _healthState.SetLastSuccessfulPoll(now);
                Telemetry.DataChanged.Add(1,
                    new KeyValuePair<string, object?>("location", result.Location),
                    new KeyValuePair<string, object?>("model", result.ModelId));
                var updated = _state.States[key];
                _log.Debug(
                    "Data changed for {Location}/{Model} - phase={Phase}, cycle={Cycle}",
                    result.Location, result.ModelId, updated.Phase, updated.Cycle);
                ScheduleNext(result.Location, result.ModelId);
                Sender.Tell(new Ack());
                RecordPollCompletion(result.Location, changed: true);
            });
        }
        else
        {
            _state = updatedState;
            _healthState.SetLastSuccessfulPoll(now);
            var updated = _state.States[key];
            _log.Debug(
                "Hash unchanged for {Location}/{Model} - miss={Miss}, phase={Phase}",
                result.Location, result.ModelId, updated.MissCount, updated.Phase);
            ScheduleNext(result.Location, result.ModelId);
            Sender.Tell(new Ack());
            RecordPollCompletion(result.Location, changed: false);
        }
    }

    private void OnFetchForecastFailed(FetchForecastFailed msg)
    {
        var key = Key(msg.Location, msg.ModelId);
        var now = _timeProvider.GetUtcNow();

        switch (msg.Reason)
        {
            case PollFailureReason.Transport:
                _state = _state.ApplyTransientFailure(key, now, _options.DiscoveryInterval);
                _log.Warning("Fetch failed for {Location}/{Model} (transport) - tfc={Tfc}, details={Details}",
                    msg.Location, msg.ModelId, _state.States[key].TransientFailureCount, msg.Detail);
                ScheduleNext(msg.Location, msg.ModelId);
                break;

            case PollFailureReason.RateLimited:
                _state = _state.ApplyRateLimited(key, now, RateLimitMinDelay, _options.DiscoveryInterval);
                _log.Warning("Fetch rate-limited for {Location}/{Model} - next poll at {Next}",
                    msg.Location, msg.ModelId, _state.States[key].NextPollUtc);
                ScheduleNext(msg.Location, msg.ModelId);
                break;

            case PollFailureReason.ModelUnavailable:
            case PollFailureReason.MalformedPayload:
                _state = _state.ApplyTransientFailure(key, now, _options.DiscoveryInterval);
                _log.Warning("Fetch failed for {Location}/{Model} ({Reason}: {Detail}) - retry scheduled",
                    msg.Location, msg.ModelId, msg.Reason, msg.Detail);
                ScheduleNext(msg.Location, msg.ModelId);
                break;
        }
    }

    private void RecordPollCompletion(string location, bool changed)
    {
        var matchedLocation = _options.FindLocation(location);
        var total = matchedLocation is null
            ? 0
            : _options.Models.Union(matchedLocation.Models ?? [], StringComparer.OrdinalIgnoreCase).Count();
        if (total == 0)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var tracker = _pollCycles.TryGetValue(location, out var existing)
            ? existing
            : new PollCycleTracker(now, 0, 0);

        tracker = tracker with
        {
            Changed = tracker.Changed + (changed ? 1 : 0),
            Reported = tracker.Reported + 1,
        };

        if (tracker.Reported >= total)
        {
            var durationSeconds = (now - tracker.Start).TotalSeconds;
            var locationTag = new KeyValuePair<string, object?>("location", location);
            Telemetry.PollCycleDuration.Record(durationSeconds, locationTag);
            Telemetry.PollCycleModels.Record(tracker.Reported, locationTag);
            _log.Debug(
                "Poll complete for {Location}: {Changed}/{Total} models changed in {Duration}ms",
                location, tracker.Changed, total, durationSeconds * 1000);
            _pollCycles.Remove(location);
        }
        else
        {
            _pollCycles[location] = tracker;
        }
    }

    private void OnQueryPollStates(QueryPollStates _)
    {
        Sender.Tell(_state.GetSnapshot());
    }

    private void ScheduleNext(string location, string modelId)
    {
        var key = Key(location, modelId);
        if (!_state.States.TryGetValue(key, out var state))
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var msg = new ScheduledPoll(location, modelId);

        _log.Debug("Scheduling next poll for {Location}/{Model} at {NextPoll}", location, modelId, state.NextPollUtc);

        if (state.NextPollUtc <= now)
        {
            Self.Tell(msg);
        }
        else
        {
            Context.System.Scheduler.ScheduleTellOnceCancelable(
                state.NextPollUtc - now, Self, msg, Self);
        }
    }

    private static string Key(string location, string modelId) => $"{location}|{modelId}";
}
