namespace Njord.Messages.Mqtt;

public sealed record MqttInboundMessage(string Topic, string Payload);
