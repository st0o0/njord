namespace Njord.Mqtt.Actors;

internal static class MqttConnectionStateExtensions
{
    public static MqttConnectionState Apply(this MqttConnectionState state, ConnectionEstablished _) =>
        state with
        {
            Session = state.Session + 1,
            ConnectAttempts = 0,
            OnlineAttempts = 0,
            IsConnected = true,
            DisconnectedSince = null,
        };

    public static MqttConnectionState Apply(this MqttConnectionState state, ConnectionLost evt) =>
        state with { IsConnected = false, DisconnectedSince = evt.Utc };

    public static MqttConnectionState Apply(this MqttConnectionState state, ReconnectScheduled _) =>
        state with { ConnectAttempts = state.ConnectAttempts + 1 };

    public static MqttConnectionState Apply(this MqttConnectionState state, OnlineAcknowledged _) =>
        state with { OnlineAttempts = 0 };

    public static MqttConnectionState Apply(this MqttConnectionState state, OnlineRejected _) =>
        state with { OnlineAttempts = state.OnlineAttempts + 1 };
}
