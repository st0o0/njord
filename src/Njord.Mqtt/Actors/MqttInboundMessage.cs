namespace Njord.Mqtt.Actors;

internal sealed record MqttInboundMessage(string Topic, string Payload);
