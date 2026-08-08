namespace Njord.Health;

public sealed class NjordHealthState
{
    private long _mqttConnectedSinceTicks;
    private long _mqttDisconnectedSinceTicks;
    private int _isMqttConnected;
    private long _lastSuccessfulPollTicks;
    private long _budgetUsedDaily;
    private long _budgetUsedMonthly;
    private long _budgetLimitDaily;
    private long _budgetLimitMonthly;

    public DateTimeOffset ServiceStartedUtc { get; init; }

    public bool IsMqttConnected => Interlocked.CompareExchange(ref _isMqttConnected, 0, 0) == 1;

    public DateTimeOffset? MqttConnectedSince
    {
        get
        {
            var ticks = Interlocked.Read(ref _mqttConnectedSinceTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public DateTimeOffset? MqttDisconnectedSince
    {
        get
        {
            var ticks = Interlocked.Read(ref _mqttDisconnectedSinceTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public DateTimeOffset? LastSuccessfulPollUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastSuccessfulPollTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public void SetMqttConnected(DateTimeOffset utcNow)
    {
        Interlocked.Exchange(ref _mqttConnectedSinceTicks, utcNow.UtcTicks);
        Interlocked.Exchange(ref _mqttDisconnectedSinceTicks, 0);
        Interlocked.Exchange(ref _isMqttConnected, 1);
    }

    public void SetMqttDisconnected(DateTimeOffset utcNow)
    {
        Interlocked.Exchange(ref _mqttDisconnectedSinceTicks, utcNow.UtcTicks);
        Interlocked.Exchange(ref _isMqttConnected, 0);
    }

    public void SetLastSuccessfulPoll(DateTimeOffset utcNow)
    {
        Interlocked.Exchange(ref _lastSuccessfulPollTicks, utcNow.UtcTicks);
    }

    public long BudgetUsedDaily => Interlocked.Read(ref _budgetUsedDaily);
    public long BudgetUsedMonthly => Interlocked.Read(ref _budgetUsedMonthly);
    public long BudgetLimitDaily => Interlocked.Read(ref _budgetLimitDaily);
    public long BudgetLimitMonthly => Interlocked.Read(ref _budgetLimitMonthly);

    public void SetBudgetUsage(long daily, long monthly)
    {
        Interlocked.Exchange(ref _budgetUsedDaily, daily);
        Interlocked.Exchange(ref _budgetUsedMonthly, monthly);
    }

    public void SetBudgetLimits(long daily, long monthly)
    {
        Interlocked.Exchange(ref _budgetLimitDaily, daily);
        Interlocked.Exchange(ref _budgetLimitMonthly, monthly);
    }
}
