using System.Diagnostics;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Stage;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Budget;
using Njord.Messages.Forecast;

namespace Njord.OpenMeteo;

public sealed record BudgetRate(int CostPerMinute, int MaxBurst);

public interface IBudgetProvider
{
    BudgetRate GetCurrentRate();
}

internal sealed class OptionsBudgetProvider : IBudgetProvider
{
    private readonly IOptions<PollingOptions> _options;

    public OptionsBudgetProvider(IOptions<PollingOptions> options)
    {
        _options = options;
    }

    public BudgetRate GetCurrentRate()
    {
        var budget = BudgetCalculator.GetEffectiveBudget(_options.Value);
        var costPerMinute = (int)(budget.RequestsPerMinute * 0.8);
        var maxBurst = Math.Max(costPerMinute / 30, 16);
        return new BudgetRate(costPerMinute, maxBurst);
    }
}

internal interface IGatewayBudgetGate
{
    bool TryAcquire(int weight);
    TimeSpan EstimateDelay(int weight);
}

internal sealed class GatewayBudgetGate : IGatewayBudgetGate
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    private readonly IBudgetProvider _provider;
    private readonly IActorRef _trackerActor;
    private readonly TimeProvider _timeProvider;
    private double _tokens;
    private double _tokensPerSecond;
    private int _maxBurst;
    private DateTimeOffset _lastRefill;
    private DateTimeOffset _lastRefresh;

    public GatewayBudgetGate(IBudgetProvider provider, IActorRef trackerActor, TimeProvider timeProvider)
    {
        _provider = provider;
        _trackerActor = trackerActor;
        _timeProvider = timeProvider;
        ApplyRate(provider.GetCurrentRate());
        _tokens = _maxBurst;
        _lastRefill = timeProvider.GetUtcNow();
        _lastRefresh = timeProvider.GetUtcNow();
    }

    public bool TryAcquire(int weight)
    {
        RefreshIfDue();
        Refill();

        if (_tokens < weight)
        {
            return false;
        }

        _tokens -= weight;
        _trackerActor.Tell(new RecordApiCall(weight));
        return true;
    }

    public TimeSpan EstimateDelay(int weight)
    {
        var deficit = weight - _tokens;
        if (deficit <= 0)
        {
            return TimeSpan.Zero;
        }

        var delay = TimeSpan.FromSeconds(deficit / _tokensPerSecond);
        return delay < TimeSpan.FromMilliseconds(10)
            ? TimeSpan.FromMilliseconds(10)
            : delay;
    }

    private void ApplyRate(BudgetRate rate)
    {
        _tokensPerSecond = rate.CostPerMinute / 60.0;
        _maxBurst = rate.MaxBurst;
    }

    private void Refill()
    {
        var now = _timeProvider.GetUtcNow();
        var elapsed = (now - _lastRefill).TotalSeconds;
        _lastRefill = now;
        _tokens = Math.Min(_tokens + elapsed * _tokensPerSecond, _maxBurst);
    }

    private void RefreshIfDue()
    {
        var now = _timeProvider.GetUtcNow();
        if (now - _lastRefresh < RefreshInterval)
        {
            return;
        }

        _lastRefresh = now;
        ApplyRate(_provider.GetCurrentRate());
    }
}

// Smooths short bursts, but never holds a target past its admission deadline: a target the budget cannot
// admit in time leaves as a rejection, so its requester is answered instead of waiting.
internal sealed class GatewayThrottleStage : GraphStage<FlowShape<OpenMeteoGateway.FetchTarget, OpenMeteoGateway.Admission>>
{
    private readonly IGatewayBudgetGate _gate;
    private readonly TimeProvider _timeProvider;

    public GatewayThrottleStage(IGatewayBudgetGate gate, TimeProvider timeProvider)
    {
        _gate = gate;
        _timeProvider = timeProvider;
        Shape = new FlowShape<OpenMeteoGateway.FetchTarget, OpenMeteoGateway.Admission>(In, Out);
    }

    public Inlet<OpenMeteoGateway.FetchTarget> In { get; } = new("GatewayThrottle.in");
    public Outlet<OpenMeteoGateway.Admission> Out { get; } = new("GatewayThrottle.out");
    public override FlowShape<OpenMeteoGateway.FetchTarget, OpenMeteoGateway.Admission> Shape { get; }

    protected override GraphStageLogic CreateLogic(Attributes inheritedAttributes)
        => new Logic(this);

    private sealed class Logic : TimerGraphStageLogic
    {
        private const string EmitTimerKey = "emit";

        private readonly GatewayThrottleStage _stage;
        private OpenMeteoGateway.FetchTarget? _pending;
        private bool _upstreamFinished;
        private long _parkedAt;

        public Logic(GatewayThrottleStage stage) : base(stage.Shape)
        {
            _stage = stage;

            SetHandler(stage.In, onPush: () => Decide(Grab(_stage.In)), onUpstreamFinish: () =>
            {
                if (_pending is null)
                {
                    CompleteStage();
                }
                else
                {
                    _upstreamFinished = true;
                }
            });

            SetHandler(stage.Out, onPull: () =>
            {
                if (_pending is null && !_upstreamFinished)
                {
                    Pull(stage.In);
                }
            });
        }

        private void Decide(OpenMeteoGateway.FetchTarget target)
        {
            var now = _stage._timeProvider.GetUtcNow();
            if (now > target.Deadline)
            {
                Push(_stage.Out, new OpenMeteoGateway.Admission(target, FetchRejectionReason.Expired));
                return;
            }

            if (_stage._gate.TryAcquire(target.Weight))
            {
                Push(_stage.Out, new OpenMeteoGateway.Admission(target, null));
                return;
            }

            var delay = _stage._gate.EstimateDelay(target.Weight);
            if (now + delay > target.Deadline)
            {
                Push(_stage.Out, new OpenMeteoGateway.Admission(target, MissedDeadline(target, delay)));
                return;
            }

            _pending = target;
            _parkedAt = Stopwatch.GetTimestamp();
            ScheduleOnce(EmitTimerKey, delay);
        }

        protected override void OnTimer(object timerKey)
        {
            if (_pending is not { } target)
            {
                return;
            }

            if (_stage._gate.TryAcquire(target.Weight))
            {
                Telemetry.ThrottleWait.Record(Stopwatch.GetElapsedTime(_parkedAt).TotalSeconds);
                Release(new OpenMeteoGateway.Admission(target, null));
                return;
            }

            var delay = _stage._gate.EstimateDelay(target.Weight);
            if (_stage._timeProvider.GetUtcNow() + delay > target.Deadline)
            {
                Release(new OpenMeteoGateway.Admission(target, MissedDeadline(target, delay)));
                return;
            }

            ScheduleOnce(EmitTimerKey, delay);
        }

        // Budget only when the token wait alone would not fit the window counted from receipt; a target whose window
        // ran out while it queued behind slow fetches has expired.
        private static FetchRejectionReason MissedDeadline(OpenMeteoGateway.FetchTarget target, TimeSpan delay) =>
            target.Cycle.Timestamp + delay > target.Deadline ? FetchRejectionReason.Budget : FetchRejectionReason.Expired;

        // A parked target is only ever held while downstream has pulled, so the outlet is available here.
        private void Release(OpenMeteoGateway.Admission admission)
        {
            _pending = null;
            Push(_stage.Out, admission);
            if (_upstreamFinished)
            {
                CompleteStage();
            }
        }
    }
}
