using Akka.Streams;

namespace Njord.Mqtt;

public sealed record MqttEgressTuning(TimeSpan ReconnectDelay)
{
    public static MqttEgressTuning Default { get; } = new(TimeSpan.FromSeconds(5));
}

public sealed record RequestMqttSink;

public sealed record MqttSinkResponse(ISinkRef<MqttMessage> SinkRef);
