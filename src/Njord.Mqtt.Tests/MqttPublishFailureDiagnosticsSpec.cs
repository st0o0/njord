using System.Diagnostics.Metrics;
using Akka.Event;
using MQTTnet.Exceptions;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttPublishFailureDiagnosticsSpec : MqttPublishFailureSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_publish_is_logged_as_a_warning_naming_the_topic()
    {
        var warnings = CreateTestProbe();
        Sys.EventStream.Subscribe(warnings, typeof(Warning));
        var transport = new ScriptedPublishTransport();
        transport.FailNext(new MqttCommunicationException("broker went away"));
        var hub = await StartPublisherAsync(transport);

        hub.Emit(Frost("lucerne", 4));

        var warning = await warnings.FishForMessageAsync<Warning>(
            w => w.Message.ToString()?.Contains("MQTT publish to njord/lucerne/alerts/frost failed") == true,
            TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("broker went away", Assert.IsType<MqttCommunicationException>(warning.Cause).Message);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_publish_is_counted_per_location_and_not_as_published()
    {
        var location = $"failcount{Guid.NewGuid():N}";
        var failures = 0L;
        var published = 0L;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Njord.Mqtt")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var tagList = tags.ToArray();
            if (!tagList.Any(t => t.Key == "location" && Equals(t.Value, location)))
            {
                return;
            }

            if (instrument.Name == "njord_mqtt_publish_failures_total")
            {
                Interlocked.Add(ref failures, value);
            }
            else if (instrument.Name == "njord_mqtt_dedup_total"
                     && tagList.Any(t => t is { Key: "decision", Value: "published" }))
            {
                Interlocked.Add(ref published, value);
            }
        });
        listener.Start();

        var transport = new ScriptedPublishTransport();
        transport.FailNext(new MqttClientNotConnectedException());
        var hub = await StartPublisherAsync(transport);

        hub.Emit(Frost(location, 1));
        await transport.WaitForAttempts(1).WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);

        await AwaitAssertAsync(() =>
        {
            Assert.Equal(1, Interlocked.Read(ref failures));
            Assert.Equal(0, Interlocked.Read(ref published));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
    }
}
