using Akka.Streams;

namespace Njord.Mqtt;

public sealed record MqttSinkResponse(ISinkRef<MqttMessage> SinkRef);
