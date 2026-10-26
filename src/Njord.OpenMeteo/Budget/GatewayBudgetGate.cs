using Akka.Actor;
using Njord.Messages.Budget;

namespace Njord.OpenMeteo.Budget;

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
