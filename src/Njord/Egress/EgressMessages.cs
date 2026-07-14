using Akka.Streams;
using Njord.Mqtt;

namespace Njord.Egress;

/// <summary>Timing knobs, overridable in tests.</summary>
public sealed record MqttEgressTuning(TimeSpan ReconnectDelay)
{
    public static MqttEgressTuning Default { get; } = new(TimeSpan.FromSeconds(5));
}

public sealed record RequestMqttSink;

public sealed record MqttSinkResponse(ISinkRef<MqttMessage> SinkRef);
