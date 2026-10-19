namespace Njord.Mqtt.Health;

public sealed class MqttSessionCounter
{
    private int _session;

    public int Session => Volatile.Read(ref _session);

    public void Increment() => Interlocked.Increment(ref _session);
}
