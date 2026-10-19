namespace Njord.Mqtt.Health;

public sealed record QueryMqttConnectionStatus;

public sealed record MqttConnectionStatusResult(bool IsConnected, DateTimeOffset? DisconnectedSince);
