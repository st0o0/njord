namespace Njord.Egress;

/// <summary>Timing knobs, overridable in tests.</summary>
public sealed record MqttEgressTuning(TimeSpan ReconnectDelay)
{
    public static MqttEgressTuning Default { get; } = new(TimeSpan.FromSeconds(5));
}
