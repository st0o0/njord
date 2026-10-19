using MQTTnet.Exceptions;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttStatusSubscriptionSpec : MqttConnectionSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_status_subscription_keeps_the_connection_and_publishes_online()
    {
        var connection = new ScriptedMqttConnection();
        connection.FailNextSubscribe(new MqttCommunicationException("simulated subscribe failure"));
        var transport = new ScriptedAvailabilityTransport();

        _ = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(1, connection.ConnectCalls),
            () => Assert.Equal(1, connection.SubscribeCalls));
    }
}
