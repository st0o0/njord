using Njord.Mqtt.Actors;

namespace Njord.Mqtt.Tests;

public sealed class MqttConnectionStateSpec
{
    private static readonly DateTimeOffset Lost = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Establishing_a_connection_starts_a_new_session_and_resets_attempts()
    {
        var state = MqttConnectionState.Empty
            .Apply(new ReconnectScheduled())
            .Apply(new OnlineRejected())
            .Apply(new ConnectionLost(Lost))
            .Apply(new ConnectionEstablished());

        Assert.Equal(new MqttConnectionState(true, null, 0, 0, 1), state);
    }

    [Fact]
    public void Losing_the_connection_records_when_and_keeps_the_session()
    {
        var state = MqttConnectionState.Empty.Apply(new ConnectionEstablished()).Apply(new ConnectionLost(Lost));

        Assert.Equal(new MqttConnectionState(false, Lost, 0, 0, 1), state);
    }

    [Fact]
    public void Reconnecting_after_a_loss_increments_the_session()
    {
        var state = MqttConnectionState.Empty
            .Apply(new ConnectionEstablished())
            .Apply(new ConnectionLost(Lost))
            .Apply(new ConnectionEstablished());

        Assert.Equal(2, state.Session);
    }

    [Fact]
    public void Each_scheduled_reconnect_counts_one_attempt()
    {
        var state = MqttConnectionState.Empty.Apply(new ReconnectScheduled()).Apply(new ReconnectScheduled());

        Assert.Equal(2, state.ConnectAttempts);
    }

    [Fact]
    public void A_rejected_online_publish_counts_and_an_acknowledged_one_resets()
    {
        var failed = MqttConnectionState.Empty.Apply(new OnlineRejected()).Apply(new OnlineRejected());
        var recovered = failed.Apply(new OnlineAcknowledged());

        Assert.Multiple(
            () => Assert.Equal(2, failed.OnlineAttempts),
            () => Assert.Equal(0, recovered.OnlineAttempts));
    }
}
