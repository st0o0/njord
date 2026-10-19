using Akka.Actor;
using Njord.Mqtt.Actors;
using Njord.Mqtt.Health;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttReconnectSpec : MqttConnectionSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_connect_that_also_signals_a_disconnect_schedules_one_reconnect()
    {
        var attempts = CreateTestProbe();
        var connection = new ScriptedMqttConnection(attempts.Ref) { FailConnectCount = 2 };
        var transport = new ScriptedAvailabilityTransport();

        _ = CreateActor(connection, transport);

        attempts.ExpectMsg<ConnectAttempted>(m => m.Attempt == 1, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        attempts.ExpectMsg<ConnectAttempted>(m => m.Attempt == 2, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        attempts.ExpectMsg<ConnectAttempted>(m => m.Attempt == 3, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);
        attempts.ExpectNoMsg(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(3, connection.ConnectCalls),
            () => Assert.Equal(0, connection.RejectedWhileConnected));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_disconnect_timestamp_survives_failed_reconnect_attempts()
    {
        var attempts = CreateTestProbe();
        var connection = new ScriptedMqttConnection(attempts.Ref);
        var transport = new ScriptedAvailabilityTransport();
        var actor = CreateActor(connection, transport);
        attempts.ExpectMsg<ConnectAttempted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        connection.FailNextConnects(3);
        connection.SimulateDisconnect();
        attempts.ExpectMsg<ConnectAttempted>(m => m.Attempt == 2, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Time.Advance(TimeSpan.FromMinutes(3));
        attempts.ExpectMsg<ConnectAttempted>(m => m.Attempt == 3, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        attempts.ExpectMsg<ConnectAttempted>(m => m.Attempt == 4, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);

        var mqtt = await actor.Ask<MqttConnectionStatusResult>(
            new QueryMqttConnectionStatus(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.False(mqtt.IsConnected),
            () => Assert.Equal(Epoch, mqtt.DisconnectedSince));
    }
}
