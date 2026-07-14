namespace Njord.Health;

public sealed class NjordHealthState
{
    private long _mqttConnectedSinceTicks;
    private long _mqttDisconnectedSinceTicks;
    private int _isMqttConnected;
    private long _lastSuccessfulPollTicks;

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
}
