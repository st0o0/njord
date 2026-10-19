using Akka.Actor;
using MQTTnet.Exceptions;
using Njord.Mqtt.Actors;
using Njord.Mqtt.Health;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttConnectionSpec : MqttConnectionSpecBase
{
    private static Task<MqttConnectionStatusResult> QueryStatus(IActorRef actor, CancellationToken ct)
        => actor.Ask<MqttConnectionStatusResult>(new QueryMqttConnectionStatus(), TimeSpan.FromSeconds(3), ct);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_connected_actor_publishes_online_and_reports_connected()
    {
        var connection = new ScriptedMqttConnection();
        var transport = new ScriptedAvailabilityTransport();

        var actor = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        var status = await QueryStatus(actor, TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.True(status.IsConnected),
            () => Assert.Null(status.DisconnectedSince),
            () => Assert.Equal(1, connection.SubscribeCalls));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_connect_is_retried_until_it_succeeds()
    {
        var connection = new ScriptedMqttConnection { FailConnectCount = 2 };
        var transport = new ScriptedAvailabilityTransport();

        _ = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(3, connection.ConnectCalls),
            () => Assert.Equal(0, connection.RejectedWhileConnected));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_lost_connection_reconnects_and_publishes_online_again()
    {
        var connection = new ScriptedMqttConnection();
        var transport = new ScriptedAvailabilityTransport();
        var actor = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        connection.SimulateDisconnect();
        await WaitForOnline(transport, TestContext.Current.CancellationToken, count: 2);

        var status = await QueryStatus(actor, TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.True(status.IsConnected),
            () => Assert.Equal(2, connection.ConnectCalls));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_disconnect_time_is_reported_while_reconnecting_fails()
    {
        var attempts = CreateTestProbe();
        var connection = new ScriptedMqttConnection(attempts.Ref);
        var transport = new ScriptedAvailabilityTransport();
        var actor = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        connection.FailNextConnects(100);
        connection.SimulateDisconnect();
        attempts.FishForMessage<ConnectAttempted>(m => m.Attempt >= 3, TestTimeouts.AwaitAssertMax,
            cancellationToken: TestContext.Current.CancellationToken);

        var status = await QueryStatus(actor, TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.False(status.IsConnected),
            () => Assert.Equal(Epoch, status.DisconnectedSince));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_status_subscription_keeps_the_connection()
    {
        var connection = new ScriptedMqttConnection();
        connection.FailNextSubscribe(new MqttCommunicationException("simulated subscribe failure"));
        var transport = new ScriptedAvailabilityTransport();

        var actor = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        var status = await QueryStatus(actor, TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.True(status.IsConnected),
            () => Assert.Equal(1, connection.ConnectCalls));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Inbound_messages_are_forwarded_to_subscribers()
    {
        var connection = new ScriptedMqttConnection();
        var transport = new ScriptedAvailabilityTransport();
        var actor = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);
        var listener = CreateTestProbe();

        actor.Tell(new SubscribeInbound(), listener.Ref);
        await actor.Ask<MqttConnectionStatusResult>(
            new QueryMqttConnectionStatus(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        connection.SimulateInbound("homeassistant/status", "online");

        var received = listener.ExpectMsg<MqttInboundMessage>(TestTimeouts.AwaitAssertMax,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("homeassistant/status", received.Topic),
            () => Assert.Equal("online", received.Payload));
    }
}
