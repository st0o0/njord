namespace Njord.Mqtt;

public sealed record MqttMessage(string Topic, string Payload, bool Retain);
