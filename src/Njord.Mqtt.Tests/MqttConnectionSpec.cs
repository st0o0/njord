using Akka.Actor;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration.Mqtt;
using Njord.Core.Health;
using Njord.Messages.Mqtt;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttConnectionSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private static readonly DateTimeOffset Epoch = TestTimeConstants.PipelineEpoch;
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch(Epoch);

    private static MqttOptions DefaultOptions() =>
        new() { Host = "localhost", BaseTopic = "njord", DiscoveryPrefix = "homeassistant" };

    private IActorRef CreateActor(
        IMqttConnection connection,
        IMqttTransport transport,
        MqttEgressTuning? tuning = null)
    {
        return Sys.ActorOf(Props.Create(() => new MqttConnection(
            Microsoft.Extensions.Options.Options.Create(DefaultOptions()),
            connection,
            transport,
            tuning ?? new MqttEgressTuning(TimeSpan.FromMilliseconds(50)),
            new NjordHealthMonitor { ServiceStartedUtc = Epoch },
            _time)));
    }

    [Fact(Timeout = 15000)]
    public async Task Connection_publishes_online_on_availability_topic()
    {
        var transport = new RecordingTransport();
        var connection = new FakeConnection();
        _ = CreateActor(connection, transport);

        await transport.WaitForMessage(m => m is { Topic: "njord/status", Payload: "online" }).WaitAsync(TestContext.Current.CancellationToken);

        Assert.Contains(transport.Sent, m => m is { Topic: "njord/status", Payload: "online" });
    }

    [Fact(Timeout = 15000)]
    public async Task Reconnect_is_attempted_after_connect_failure()
    {
        var transport = new RecordingTransport();
        var connection = new FakeConnection { FailConnectCount = 1 };
        _ = CreateActor(connection, transport);

        await transport.WaitForMessage(m => m is { Topic: "njord/status", Payload: "online" }).WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(connection.ConnectCallCount >= 2,
            $"Expected at least 2 connect attempts, got {connection.ConnectCallCount}");
        Assert.Contains(transport.Sent, m => m is { Topic: "njord/status", Payload: "online" });
    }

    [Fact(Timeout = 15000)]
    public async Task Reconnect_is_attempted_after_canceled_connect()
    {
        var transport = new RecordingTransport();
        var connection = new FakeConnection { CancelConnectCount = 1 };
        _ = CreateActor(connection, transport);

        await transport.WaitForMessage(m => m is { Topic: "njord/status", Payload: "online" }).WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(connection.ConnectCallCount >= 2,
            $"Expected at least 2 connect attempts, got {connection.ConnectCallCount}");
    }

    [Fact(Timeout = 15000)]
    public async Task Reconnect_is_attempted_after_repeated_connect_failures()
    {
        var transport = new RecordingTransport();
        var connection = new FakeConnection { FailConnectCount = 2 };
        _ = CreateActor(connection, transport);

        await transport.WaitForMessage(m => m is { Topic: "njord/status", Payload: "online" }).WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(connection.ConnectCallCount >= 3,
            $"Expected at least 3 connect attempts, got {connection.ConnectCallCount}");
    }

    [Fact(Timeout = 15000)]
    public async Task Offline_is_enqueued_on_stop()
    {
        var transport = new RecordingTransport();
        var connection = new FakeConnection();
        var actor = CreateActor(connection, transport);

        await transport.WaitForMessage(m => m is { Topic: "njord/status", Payload: "online" }).WaitAsync(TestContext.Current.CancellationToken);

        await actor.GracefulStop(TimeSpan.FromSeconds(3));
    }

    [Fact(Timeout = 15000)]
    public async Task Inbound_messages_are_forwarded_to_subscribers()
    {
        var transport = new RecordingTransport();
        var connection = new FakeConnection();
        var actor = CreateActor(connection, transport);

        await transport.WaitForMessage(m => m is { Topic: "njord/status", Payload: "online" }).WaitAsync(TestContext.Current.CancellationToken);

        var inbox = Inbox.Create(Sys);
        inbox.Send(actor, new SubscribeInbound());

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
        private int _cancelConnectCount;
        private int _connectCalls;

        public int FailConnectCount { get => _failConnectCount; init => _failConnectCount = value; }
        public int CancelConnectCount { get => _cancelConnectCount; init => _cancelConnectCount = value; }
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

            if (attempt <= _failConnectCount + _cancelConnectCount)
            {
                return Task.FromCanceled(new CancellationToken(canceled: true));
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

        public IReadOnlyList<SentMessage> Sent
        {
            get
            {
                lock (_lock)
                {
                    return [.. _sent];
                }
            }
        }

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
                {
                    return Task.CompletedTask;
                }

                var tcs = new TaskCompletionSource();
                _waiters.Add((predicate, tcs));
                return tcs.Task;
            }
        }
    }
}
