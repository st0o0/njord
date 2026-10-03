using Akka.Streams;

namespace Njord.Mqtt;

public sealed record MqttSinkResponse(long RequestId, ISinkRef<MqttMessage> SinkRef);

public sealed record MqttSinkFailed(long RequestId, Exception Cause);
