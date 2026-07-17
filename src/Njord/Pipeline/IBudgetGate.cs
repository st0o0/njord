using Njord.Configuration;

namespace Njord.Pipeline;

public interface IBudgetGate<in T>
{
    Task AcquireAsync(T element, CancellationToken ct = default);
}

public sealed class WeightedBudgetGate : IBudgetGate<WeightedTarget>
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    private readonly IBudgetProvider _provider;
    private readonly BudgetTracker _tracker;
    private readonly object _lock = new();
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

    public async Task AcquireAsync(WeightedTarget element, CancellationToken ct = default)
    {
        var cost = element.Weight;

        while (true)
        {
            TimeSpan delay;
            lock (_lock)
            {
                RefreshIfDue();
                Refill();

                if (_tokens >= cost)
                {
                    _tokens -= cost;
                    _tracker.RecordCall(cost);
                    return;
                }

                var deficit = cost - _tokens;
                delay = TimeSpan.FromSeconds(deficit / _tokensPerSecond);
                if (delay < TimeSpan.FromMilliseconds(10))
                    delay = TimeSpan.FromMilliseconds(10);
            }

            await Task.Delay(delay, ct);
        }
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
