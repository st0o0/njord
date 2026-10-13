using System.Diagnostics;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Stage;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Pipeline;

namespace Njord.OpenMeteo;

public sealed record BudgetRate(int CostPerMinute, int MaxBurst);

public interface IBudgetProvider
{
    BudgetRate GetCurrentRate();
}

// Reads the budget per call so a hot budget change reaches the gate without a subscription (design D5).
internal sealed class OptionsBudgetProvider : IBudgetProvider
{
    private readonly IOptionsMonitor<PollingOptions> _options;

    public OptionsBudgetProvider(IOptionsMonitor<PollingOptions> options)
    {
        _options = options;
    }

    public BudgetRate GetCurrentRate()
    {
        var budget = BudgetCalculator.GetEffectiveBudget(_options.CurrentValue);
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
            return false;

        _tokens -= weight;
        _trackerActor.Tell(new RecordApiCall(weight));
        return true;
    }

    public TimeSpan EstimateDelay(int weight)
    {
        var deficit = weight - _tokens;
        if (deficit <= 0)
            return TimeSpan.Zero;

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
            return;

        _lastRefresh = now;
        ApplyRate(_provider.GetCurrentRate());
    }
}

internal sealed class GatewayThrottleStage : GraphStage<FlowShape<OpenMeteoGateway.FetchTarget, OpenMeteoGateway.FetchTarget>>
{
    private readonly IGatewayBudgetGate _gate;

    public GatewayThrottleStage(IGatewayBudgetGate gate)
    {
        _gate = gate;
        Shape = new FlowShape<OpenMeteoGateway.FetchTarget, OpenMeteoGateway.FetchTarget>(In, Out);
    }

    public Inlet<OpenMeteoGateway.FetchTarget> In { get; } = new("GatewayThrottle.in");
    public Outlet<OpenMeteoGateway.FetchTarget> Out { get; } = new("GatewayThrottle.out");
    public override FlowShape<OpenMeteoGateway.FetchTarget, OpenMeteoGateway.FetchTarget> Shape { get; }

    protected override GraphStageLogic CreateLogic(Attributes inheritedAttributes)
        => new Logic(this);

    private sealed class Logic : TimerGraphStageLogic
    {
        private const string EmitTimerKey = "emit";

        private readonly GatewayThrottleStage _stage;
        private OpenMeteoGateway.FetchTarget? _pending;
        private bool _hasPending;
        private bool _upstreamFinished;
        private long _parkedAt;

        public Logic(GatewayThrottleStage stage) : base(stage.Shape)
        {
            _stage = stage;

            SetHandler(stage.In, onPush: OnPush, onUpstreamFinish: () =>
            {
                if (!_hasPending)
                    CompleteStage();
                else
                    _upstreamFinished = true;
            });

            SetHandler(stage.Out, onPull: () =>
            {
                if (!_hasPending && !_upstreamFinished)
                    Pull(stage.In);
            });
        }

        private void OnPush()
        {
            var element = Grab(_stage.In);

            if (_stage._gate.TryAcquire(element.Weight))
            {
                Push(_stage.Out, element);
            }
            else
            {
                _pending = element;
                _hasPending = true;
                _parkedAt = Stopwatch.GetTimestamp();
                ScheduleOnce(EmitTimerKey, _stage._gate.EstimateDelay(element.Weight));
            }
        }

        protected override void OnTimer(object timerKey)
        {
            if (!_hasPending)
                return;

            if (_stage._gate.TryAcquire(_pending!.Weight))
            {
                Telemetry.ThrottleWait.Record(Stopwatch.GetElapsedTime(_parkedAt).TotalSeconds);
                Push(_stage.Out, _pending!);
                _pending = null;
                _hasPending = false;

                if (_upstreamFinished)
                {
                    CompleteStage();
                    return;
                }

                if (IsAvailable(_stage.Out))
                    Pull(_stage.In);
            }
            else
            {
                ScheduleOnce(EmitTimerKey, _stage._gate.EstimateDelay(_pending!.Weight));
            }
        }
    }
}
