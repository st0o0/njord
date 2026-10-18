using System.Diagnostics.Metrics;
using System.Text.Json.Nodes;
using Akka.Actor;
using Akka.Streams;
using MQTTnet.Exceptions;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Messages.Pipeline;
using Njord.Messages.Trends;
using Njord.Mqtt.Actors;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Mqtt.Tests;

public sealed class MqttPublishFailureSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;

    private static readonly MqttEgressTuning FastTimeout =
        MqttEgressTuning.Default with { PublishTimeout = TimeSpan.FromMilliseconds(200) };

    private IActorRef CreatePublisher(IMqttTransport transport)
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2"],
        };

        var parameters = ParameterSelection.Resolve(["Weather"], [], []);
        var timeProvider = TestTimeouts.AtEpoch(Anchor);

        return Sys.ActorOf(Props.Create(() => new MqttStatePublisher(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(new MqttOptions { BaseTopic = "njord" }),
            parameters,
            timeProvider,
            transport,
            FastTimeout)), $"mqtt-state-{Guid.NewGuid():N}");
    }

    private async Task<MqttStateSpec.FakeSourceHub> StartPublisherAsync(IMqttTransport transport)
    {
        var hub = new MqttStateSpec.FakeSourceHub(Sys.Materializer());
        ActorRegistry.Register<IForecastPipeline>(hub.Actor(Sys, typeof(RequestPipelineOutput)), overwrite: true);
        CreatePublisher(transport);
        await hub.WaitForQueue().WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        return hub;
    }

    private static PipelineEvent.TrendComputed Trend(string location, int precipStarts) => new(
        new TrendResult(
            new LocationId(location),
            new Dictionary<string, ParameterTrend?>(),
            null,
            new PrecipTimingInfo(precipStarts, precipStarts + 3),
            new ExtremaTimingInfo(null, null),
            null,
            null),
        Anchor);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Later_events_are_still_published_after_a_failed_publish()
    {
        var transport = new ScriptedTransport();
        transport.FailNext(new MqttClientNotConnectedException());
        var hub = await StartPublisherAsync(transport);

        hub.Emit(Trend("lucerne", 1));
        await transport.WaitForAttempts(1).WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        hub.Emit(Trend("zurich", 2));

        await transport.WaitForSent(m => m.Topic == "njord/zurich/trends")
            .WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        var sent = Assert.Single(transport.Sent);
        var payload = JsonNode.Parse(sent.Payload);
        Assert.NotNull(payload);
        Assert.Equal(2, payload["precip_starts"]!.GetValue<int>());
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_payload_whose_publish_failed_is_published_again_on_the_next_cycle()
    {
        var transport = new ScriptedTransport();
        transport.FailNext(new MqttCommunicationException("broker went away"));
        var hub = await StartPublisherAsync(transport);

        hub.Emit(Trend("lucerne", 4));
        await transport.WaitForAttempts(1).WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        hub.Emit(Trend("lucerne", 4));

        await transport.WaitForSent(m => m.Topic == "njord/lucerne/trends")
            .WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.Equal(2, transport.Attempts),
            () => Assert.Equal("njord/lucerne/trends", Assert.Single(transport.Sent).Topic));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_publish_does_not_cancel_the_pipeline_subscription()
    {
        var transport = new ScriptedTransport();
        transport.FailNext(new MqttClientNotConnectedException());
        var hub = await StartPublisherAsync(transport);

        hub.Emit(Trend("lucerne", 1));
        await transport.WaitForAttempts(1).WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        hub.Emit(Trend("lucerne", 2));
        await transport.WaitForSent(m => m.Topic == "njord/lucerne/trends")
            .WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);

        Assert.False(hub.Completion.IsCompleted, "the pipeline output stream was cancelled by the MQTT consumer");
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_hung_publish_times_out_and_the_next_message_is_published()
    {
        var transport = new ScriptedTransport();
        transport.HangNext();
        var hub = await StartPublisherAsync(transport);

        hub.Emit(Trend("lucerne", 1));
        await transport.WaitForAttempts(1).WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        hub.Emit(Trend("zurich", 2));

        await transport.WaitForSent(m => m.Topic == "njord/zurich/trends")
            .WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
        Assert.Equal("njord/zurich/trends", Assert.Single(transport.Sent).Topic);
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
                     && tagList.Any(t => t.Key == "decision" && Equals(t.Value, "published")))
            {
                Interlocked.Add(ref published, value);
            }
        });
        listener.Start();

        var transport = new ScriptedTransport();
        transport.FailNext(new MqttClientNotConnectedException());
        var hub = await StartPublisherAsync(transport);

        hub.Emit(Trend(location, 1));
        await transport.WaitForAttempts(1).WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);

        await AwaitAssertAsync(() =>
        {
            Assert.Equal(1, Interlocked.Read(ref failures));
            Assert.Equal(0, Interlocked.Read(ref published));
        }, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
    }

    internal sealed class ScriptedTransport : IMqttTransport
    {
        private readonly Queue<Func<CancellationToken, Task>> _script = new();
        private readonly List<MqttStateSpec.SentMessage> _sent = [];
        private readonly List<(Func<int, bool> Predicate, TaskCompletionSource Tcs)> _attemptWaiters = [];
        private readonly List<(Func<MqttStateSpec.SentMessage, bool> Predicate, TaskCompletionSource Tcs)> _sentWaiters = [];
        private readonly object _lock = new();
        private int _attempts;

        public int Attempts { get { lock (_lock) { return _attempts; } } }

        public IReadOnlyList<MqttStateSpec.SentMessage> Sent { get { lock (_lock) { return [.. _sent]; } } }

        public void FailNext(Exception cause)
        {
            lock (_lock) { _script.Enqueue(_ => Task.FromException(cause)); }
        }

        public void HangNext()
        {
            lock (_lock) { _script.Enqueue(ct => Task.Delay(Timeout.Infinite, ct)); }
        }

        public Task SendAsync(string topic, string payload, bool retain, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task>? step;
            lock (_lock)
            {
                _attempts++;
                Notify(_attemptWaiters, _attempts);
                _script.TryDequeue(out step);
                if (step is null)
                {
                    var msg = new MqttStateSpec.SentMessage(topic, payload, retain);
                    _sent.Add(msg);
                    Notify(_sentWaiters, msg);
                }
            }

            return step is null ? Task.CompletedTask : step(cancellationToken);
        }

        public Task WaitForAttempts(int count)
        {
            lock (_lock)
            {
                return _attempts >= count ? Task.CompletedTask : Register(_attemptWaiters, n => n >= count);
            }
        }

        public Task WaitForSent(Func<MqttStateSpec.SentMessage, bool> predicate)
        {
            lock (_lock)
            {
                return _sent.Any(predicate) ? Task.CompletedTask : Register(_sentWaiters, predicate);
            }
        }

        private static Task Register<T>(List<(Func<T, bool>, TaskCompletionSource)> waiters, Func<T, bool> predicate)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            waiters.Add((predicate, tcs));
            return tcs.Task;
        }

        private static void Notify<T>(List<(Func<T, bool> Predicate, TaskCompletionSource Tcs)> waiters, T value)
        {
            for (var i = waiters.Count - 1; i >= 0; i--)
            {
                if (waiters[i].Predicate(value))
                {
                    waiters[i].Tcs.TrySetResult();
                    waiters.RemoveAt(i);
                }
            }
        }
    }
}
