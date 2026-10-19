using System.Text.Json.Nodes;
using MQTTnet.Exceptions;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttPublishFailureRecoverySpec : MqttPublishFailureSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Later_events_are_still_published_after_a_failed_publish()
    {
        var transport = new ScriptedPublishTransport();
        transport.FailNext(new MqttClientNotConnectedException());
        var hub = await StartPublisherAsync(transport);

        hub.Emit(Frost("lucerne", 1));
        await transport.WaitForAttempts(1).WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        hub.Emit(Frost("zurich", 2));

        await transport.WaitForSent(m => m.Topic == "njord/zurich/alerts/frost")
            .WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        var sent = Assert.Single(transport.Sent);
        var payload = JsonNode.Parse(sent.Payload);
        Assert.NotNull(payload);
        Assert.Equal(2, payload["peak_value"]!.GetValue<double>());
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_payload_whose_publish_failed_is_published_again_on_the_next_cycle()
    {
        var transport = new ScriptedPublishTransport();
        transport.FailNext(new MqttCommunicationException("broker went away"));
        var hub = await StartPublisherAsync(transport);

        hub.Emit(Frost("lucerne", 4));
        await transport.WaitForAttempts(1).WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        hub.Emit(Frost("lucerne", 4));

        await transport.WaitForSent(m => m.Topic == "njord/lucerne/alerts/frost")
            .WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal(2, transport.Attempts),
            () => Assert.Equal("njord/lucerne/alerts/frost", Assert.Single(transport.Sent).Topic));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_publish_does_not_cancel_the_pipeline_subscription()
    {
        var transport = new ScriptedPublishTransport();
        transport.FailNext(new MqttClientNotConnectedException());
        var hub = await StartPublisherAsync(transport);

        hub.Emit(Frost("lucerne", 1));
        await transport.WaitForAttempts(1).WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        hub.Emit(Frost("lucerne", 2));
        await transport.WaitForSent(m => m.Topic == "njord/lucerne/alerts/frost")
            .WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);

        Assert.False(hub.Completion.IsCompleted, "the pipeline output stream was cancelled by the MQTT consumer");
    }
}
