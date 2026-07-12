namespace Njord.Egress;

public sealed record MqttMessage(string Topic, string Payload, bool Retain);
