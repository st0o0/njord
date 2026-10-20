namespace Njord.Mqtt.Actors;

internal sealed record MqttConnectionState(
    bool IsConnected,
    DateTimeOffset? DisconnectedSince,
    int ConnectAttempts,
    int OnlineAttempts,
    int Session)
{
    public static readonly MqttConnectionState Empty = new(false, null, 0, 0, 0);
}
