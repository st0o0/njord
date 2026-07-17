using Njord.Configuration;

namespace Njord.Pipeline;

public interface IBudgetGate<in T>
{
    bool TryAcquire(T element);
    TimeSpan EstimateDelay(T element);
}

public sealed class WeightedBudgetGate : IBudgetGate<WeightedTarget>
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    private readonly IBudgetProvider _provider;
    private readonly BudgetTracker _tracker;
    private double _tokens;
    private double _tokensPerSecond;
    private int _maxBurst;
    private DateTimeOffset _lastRefill;
    private DateTimeOffset _lastRefresh;

    public WeightedBudgetGate(IBudgetProvider provider, BudgetTracker tracker)
    {
        _provider = provider;
        _tracker = tracker;
        ApplyRate(provider.GetCurrentRate());
        _tokens = _maxBurst;
        _lastRefill = DateTimeOffset.UtcNow;
        _lastRefresh = DateTimeOffset.UtcNow;
    }

    public bool TryAcquire(WeightedTarget element)
    {
        RefreshIfDue();
        Refill();

        var cost = element.Weight;
        if (_tokens < cost)
            return false;

        _tokens -= cost;
        _tracker.RecordCall(cost);
        return true;
    }

    public TimeSpan EstimateDelay(WeightedTarget element)
    {
        var deficit = element.Weight - _tokens;
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
        var now = DateTimeOffset.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;
        _lastRefill = now;
        _tokens = Math.Min(_tokens + elapsed * _tokensPerSecond, _maxBurst);
    }

    private void RefreshIfDue()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastRefresh < RefreshInterval)
            return;

        _lastRefresh = now;
        ApplyRate(_provider.GetCurrentRate());
    }
}
