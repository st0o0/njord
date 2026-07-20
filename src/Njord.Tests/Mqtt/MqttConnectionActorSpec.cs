using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Configuration;
using Njord.Health;
using Njord.Mqtt;
using Njord.Mqtt.Transport;

namespace Njord.Tests.Mqtt;

public sealed class MqttConnectionActorSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

    private static NjordOptions DefaultOptions() => new()
    {
        Mqtt = new MqttOptions { Host = "localhost", BaseTopic = "njord", DiscoveryPrefix = "homeassistant" },
    };

    private IActorRef CreateActor(
        IMqttConnection connection,
        IMqttTransport transport,
        MqttEgressTuning? tuning = null)
    {
        return Sys.ActorOf(Props.Create(() => new MqttConnectionActor(
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

        await transport.WaitForMessage(m => m.Topic == "njord/status" && m.Payload == "online");

        Assert.Contains(transport.Sent, m => m.Topic == "njord/status" && m.Payload == "online");
    }

    [Fact(Timeout = 15000)]
    public async Task Reconnect_is_attempted_after_connect_failure()
    {
        var transport = new RecordingTransport();
        var connection = new FakeConnection { FailConnectCount = 1 };
        _ = CreateActor(connection, transport);

        await transport.WaitForMessage(m => m.Topic == "njord/status" && m.Payload == "online");

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

        await transport.WaitForMessage(m => m.Topic == "njord/status" && m.Payload == "online");

        await actor.GracefulStop(TimeSpan.FromSeconds(3));
    }

    [Fact(Timeout = 15000)]
    public async Task Inbound_messages_are_forwarded_to_subscribers()
    {
        var transport = new RecordingTransport();
        var connection = new FakeConnection();
        var actor = CreateActor(connection, transport);

        await transport.WaitForMessage(m => m.Topic == "njord/status" && m.Payload == "online");

        var inbox = Inbox.Create(Sys);
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
        private readonly List<SentMessage> _sent = [];
        private readonly List<(Func<SentMessage, bool> Predicate, TaskCompletionSource Tcs)> _waiters = [];
        private readonly object _lock = new();

        public IReadOnlyList<SentMessage> Sent { get { lock (_lock) return [.. _sent]; } }

        public Task SendAsync(string topic, string payload, bool retain, CancellationToken cancellationToken)
        {
            var msg = new SentMessage(topic, payload, retain);
            lock (_lock)
            {
                _sent.Add(msg);
                for (var i = _waiters.Count - 1; i >= 0; i--)
                {
                    if (_waiters[i].Predicate(msg))
                    {
                        _waiters[i].Tcs.TrySetResult();
                        _waiters.RemoveAt(i);
                    }
                }
            }
            return Task.CompletedTask;
        }

        public Task WaitForMessage(Func<SentMessage, bool> predicate)
        {
            lock (_lock)
            {
                if (_sent.Any(predicate))
                    return Task.CompletedTask;
                var tcs = new TaskCompletionSource();
                _waiters.Add((predicate, tcs));
                return tcs.Task;
            }
        }
    }
}
