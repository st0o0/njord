namespace Njord.Mqtt.Health;

public sealed record MqttConnectionStatus(bool IsConnected, DateTimeOffset? DisconnectedSince);

public sealed class MqttConnectionState
{
    private MqttConnectionStatus _current = new(false, null);
    private int _session;

    public MqttConnectionStatus Current => Volatile.Read(ref _current);

    // Counts the established connections: consumers that cache what the broker already holds start over when it changes.
    public int Session => Volatile.Read(ref _session);

    public void Connected()
    {
        Interlocked.Increment(ref _session);
        Volatile.Write(ref _current, new MqttConnectionStatus(true, null));
    }

    public void Disconnected(DateTimeOffset at) => Volatile.Write(ref _current, new MqttConnectionStatus(false, at));
}
