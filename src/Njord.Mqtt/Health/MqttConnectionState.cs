namespace Njord.Mqtt.Health;

public sealed record MqttConnectionStatus(bool IsConnected, DateTimeOffset? DisconnectedSince);

public sealed class MqttConnectionState
{
    private MqttConnectionStatus _current = new(false, null);

    public MqttConnectionStatus Current => Volatile.Read(ref _current);

    public void Connected() => Volatile.Write(ref _current, new MqttConnectionStatus(true, null));

    public void Disconnected(DateTimeOffset at) => Volatile.Write(ref _current, new MqttConnectionStatus(false, at));
}
