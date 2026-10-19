using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttPublishTimeoutSpec : MqttPublishFailureSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_hung_publish_times_out_and_the_next_message_is_published()
    {
        var transport = new ScriptedPublishTransport();
        transport.HangNext();
        var hub = await StartPublisherAsync(transport);

        hub.Emit(Frost("lucerne", 1));
        await transport.WaitForAttempts(1).WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        hub.Emit(Frost("zurich", 2));

        await transport.WaitForSent(m => m.Topic == "njord/zurich/alerts/frost")
            .WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        Assert.Equal("njord/zurich/alerts/frost", Assert.Single(transport.Sent).Topic);
    }
}
