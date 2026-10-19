using Akka.Actor;
using MQTTnet.Exceptions;
using Njord.Mqtt.Actors;
using Njord.Mqtt.Health;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttOnlinePublishRetrySpec : MqttConnectionSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_online_publish_is_retried_on_the_same_connection()
    {
        var connection = new ScriptedMqttConnection();
        var transport = new ScriptedAvailabilityTransport();
        transport.FailNextOnline(new MqttCommunicationException("simulated online publish failure"));

        var actor = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        var status = await actor.Ask<MqttConnectionStatusResult>(
            new QueryMqttConnectionStatus(), TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal(1, connection.ConnectCalls),
            () => Assert.Equal(0, connection.RejectedWhileConnected),
            () => Assert.Equal(2, transport.OnlineAttempts),
            () => Assert.DoesNotContain(transport.Sent, m => m.Payload == "offline"),
            () => Assert.True(status.IsConnected));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_online_publish_does_not_restart_the_actor()
    {
        var connection = new ScriptedMqttConnection();
        var transport = new ScriptedAvailabilityTransport();
        var actor = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        var listener = CreateTestProbe();
        listener.Send(actor, new SubscribeInbound());
        transport.FailNextOnline(new MqttCommunicationException("simulated online publish failure"));
        connection.SimulateDisconnect();
        await WaitForOnline(transport, TestContext.Current.CancellationToken, count: 2);

        connection.SimulateInbound("homeassistant/status", "online");

        listener.ExpectMsg<MqttConnected>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        var inbound = listener.ExpectMsg<MqttInboundMessage>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("homeassistant/status", inbound.Topic),
            () => Assert.Equal(2, connection.ConnectCalls),
            () => Assert.Equal(0, connection.RejectedWhileConnected),
            () => Assert.DoesNotContain(transport.Sent, m => m.Payload == "offline"));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_hung_online_publish_times_out_and_is_retried()
    {
        var connection = new ScriptedMqttConnection();
        var transport = new ScriptedAvailabilityTransport();
        transport.HangNextOnline();

        var actor = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        var listener = CreateTestProbe();
        listener.Send(actor, new SubscribeInbound());
        connection.SimulateInbound("homeassistant/status", "online");

        var inbound = listener.ExpectMsg<MqttInboundMessage>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal("online", inbound.Payload),
            () => Assert.Equal(1, connection.ConnectCalls),
            () => Assert.Equal(2, transport.OnlineAttempts));
    }
}
