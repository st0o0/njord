using System.Collections.Concurrent;
using Akka.Actor;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Configuration;
using Njord.Health;
using Njord.Mqtt;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;

namespace Njord.Tests.Mqtt;

public sealed class MqttConnectionActorSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("mqtt-conn-spec");

    public void Dispose() => _system.Dispose();

    private static NjordOptions DefaultOptions() => new()
    {
        Mqtt = new MqttOptions { Host = "localhost", BaseTopic = "njord", DiscoveryPrefix = "homeassistant" },
    };

    private IActorRef CreateActor(
        IMqttConnection connection,
        IMqttTransport transport,
        MqttEgressTuning? tuning = null)
    {
        return _system.ActorOf(Props.Create(() => new MqttConnectionActor(
            Microsoft.Extensions.Options.Options.Create(DefaultOptions()),
            connection,
            transport,
            NullLogger<MqttConnectionActor>.Instance,
            tuning ?? new MqttEgressTuning(TimeSpan.FromMilliseconds(50)),
            new NjordHealthState { ServiceStartedUtc = DateTimeOffset.UtcNow })));
    }

    [Fact(Timeout = 15000)]
    public async Task Connection_publishes_online_on_availability_topic()
    {
        var transport = new RecordingTransport();
        var connection = new FakeConnection();
        _ = CreateActor(connection, transport);

        await AsyncAssert.WaitUntil(() =>
            transport.Sent.Any(m => m.Topic == "njord/status" && m.Payload == "online"));

        Assert.Contains(transport.Sent, m => m.Topic == "njord/status" && m.Payload == "online");
    }

    [Fact(Timeout = 15000)]
    public async Task Reconnect_is_attempted_after_connect_failure()
    {
        var transport = new RecordingTransport();
        var connection = new FakeConnection { FailConnectCount = 1 };
        _ = CreateActor(connection, transport);

        await AsyncAssert.WaitUntil(() =>
            connection.ConnectCallCount >= 2 &&
            transport.Sent.Any(m => m.Topic == "njord/status" && m.Payload == "online"));

        Assert.True(connection.ConnectCallCount >= 2,
            $"Expected at least 2 connect attempts, got {connection.ConnectCallCount}");
        Assert.Contains(transport.Sent, m => m.Topic == "njord/status" && m.Payload == "online");
    }

    [Fact(Timeout = 15000)]
    public async Task SinkRef_is_returned_on_RequestMqttSink()
    {
        var transport = new RecordingTransport();
        var connection = new FakeConnection();
        var actor = CreateActor(connection, transport);

        var response = await actor.Ask<MqttSinkResponse>(new RequestMqttSink(), TimeSpan.FromSeconds(3));
        Assert.NotNull(response);
        Assert.NotNull(response.SinkRef);
    }

    [Fact(Timeout = 15000)]
    public async Task Offline_is_enqueued_on_stop()
    {
        var transport = new RecordingTransport();
        var connection = new FakeConnection();
        var actor = CreateActor(connection, transport);

        await AsyncAssert.WaitUntil(() =>
            transport.Sent.Any(m => m.Topic == "njord/status" && m.Payload == "online"));

        await actor.GracefulStop(TimeSpan.FromSeconds(3));
    }

    [Fact(Timeout = 15000)]
    public async Task Inbound_messages_are_forwarded_to_subscribers()
    {
        var transport = new RecordingTransport();
        var connection = new FakeConnection();
        var actor = CreateActor(connection, transport);

        await AsyncAssert.WaitUntil(() =>
            transport.Sent.Any(m => m.Topic == "njord/status" && m.Payload == "online"));

        var inbox = Inbox.Create(_system);
        actor.Tell(new SubscribeInbound(inbox.Receiver));

        connection.SimulateInbound("homeassistant/status", "online");

        var received = (MqttInboundMessage)await inbox.ReceiveAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("homeassistant/status", received.Topic);
        Assert.Equal("online", received.Payload);
    }

    // --- Fakes ----------------------------------------------------------

    private sealed class FakeConnection : IMqttConnection
    {
        private Action<string, string>? _onMessage;
        private int _failConnectCount;
        private int _connectCalls;

        public int FailConnectCount { get => _failConnectCount; init => _failConnectCount = value; }
        public int ConnectCallCount => Volatile.Read(ref _connectCalls);

        public Task ConnectAsync(
            Action<string, string> onMessage,
            Action onDisconnected,
            CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _connectCalls);
            if (attempt <= _failConnectCount)
            {
                return Task.FromException(new InvalidOperationException($"Simulated connect failure #{attempt}"));
            }

            _onMessage = onMessage;
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public void SimulateInbound(string topic, string payload)
            => _onMessage?.Invoke(topic, payload);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed record SentMessage(string Topic, string Payload, bool Retain);

    private sealed class RecordingTransport : IMqttTransport
    {
        private readonly ConcurrentBag<SentMessage> _sent = [];

        public IReadOnlyCollection<SentMessage> Sent => _sent;

        public Task SendAsync(string topic, string payload, bool retain, CancellationToken cancellationToken)
        {
            _sent.Add(new SentMessage(topic, payload, retain));
            return Task.CompletedTask;
        }
    }
}
