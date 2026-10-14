namespace Njord.Mqtt;

public sealed record MqttEgressTuning(TimeSpan ReconnectDelay)
{
    public static MqttEgressTuning Default { get; } = new(TimeSpan.FromSeconds(5));

    public TimeSpan PublishTimeout { get; init; } = TimeSpan.FromSeconds(10);

    // Pipeline events waiting for the broker; when it falls further behind, the oldest are dropped.
    public int EventBufferSize { get; init; } = 256;
}
