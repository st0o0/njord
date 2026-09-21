using Akka.Streams;

namespace Njord.Mqtt;

public sealed record MqttSinkResponse(long RequestId, ISinkRef<MqttMessage> SinkRef);
