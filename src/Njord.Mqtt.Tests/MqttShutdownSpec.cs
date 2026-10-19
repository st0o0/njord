using Akka.Actor;
using MQTTnet.Exceptions;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttShutdownSpec : MqttConnectionSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Stopping_publishes_offline()
    {
        var connection = new ScriptedMqttConnection();
        var transport = new ScriptedAvailabilityTransport();
        var actor = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        Assert.True(await actor.GracefulStop(TestTimeouts.AwaitAssertMax));

        await transport.WaitForSent(sent => sent.Any(m => m is { Topic: AvailabilityTopic, Payload: "offline", Retain: true }))
            .WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Stopping_completes_when_the_offline_publish_fails()
    {
        var connection = new ScriptedMqttConnection();
        var transport = new ScriptedAvailabilityTransport();
        var actor = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);
        transport.FailNextOffline(new MqttClientNotConnectedException());

        Assert.True(await actor.GracefulStop(TestTimeouts.AwaitAssertMax));
        Assert.Equal(1, transport.OfflineAttempts);
    }
}
