using Akka.Actor;
using Akka.TestKit;
using MQTTnet.Exceptions;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration.Mqtt;
using Njord.Mqtt.Actors;
using Njord.Mqtt.Health;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttConnectionLifecycleSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private const string AvailabilityTopic = "njord/status";

    private static readonly DateTimeOffset Epoch = TestTimeConstants.PipelineEpoch;
    private readonly FakeTimeProvider _time = TestTimeouts.AtEpoch(Epoch);
    private readonly MqttConnectionState _health = new();

    private static readonly MqttEgressTuning FastTuning =
        new MqttEgressTuning(TimeSpan.FromMilliseconds(50)) { PublishTimeout = TimeSpan.FromMilliseconds(200) };

    private IActorRef CreateActor(ScriptedConnection connection, ScriptedTransport transport)
        => Sys.ActorOf(Props.Create(() => new MqttConnection(
                Microsoft.Extensions.Options.Options.Create(
                    new MqttOptions { Host = "localhost", BaseTopic = "njord", DiscoveryPrefix = "homeassistant" }),
                connection,
                transport,
                FastTuning,
                _health,
                _time)),
            $"mqtt-connection-{Guid.NewGuid():N}");

    private static Task WaitForOnline(ScriptedTransport transport, CancellationToken cancellationToken, int count = 1)
        => transport.WaitForSent(sent => sent.Count(m => m is { Topic: AvailabilityTopic, Payload: "online" }) >= count)
            .WaitAsync(TestTimeouts.RealTimeWaitMax, cancellationToken);

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_online_publish_is_retried_on_the_same_connection()
    {
        var connection = new ScriptedConnection();
        var transport = new ScriptedTransport();
        transport.FailNextOnline(new MqttCommunicationException("simulated online publish failure"));

        _ = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(1, connection.ConnectCalls),
            () => Assert.Equal(0, connection.RejectedWhileConnected),
            () => Assert.Equal(2, transport.OnlineAttempts),
            () => Assert.DoesNotContain(transport.Sent, m => m.Payload == "offline"),
            () => Assert.True(_health.Current.IsConnected));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_online_publish_does_not_restart_the_actor()
    {
        var connection = new ScriptedConnection();
        var transport = new ScriptedTransport();
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
        var connection = new ScriptedConnection();
        var transport = new ScriptedTransport();
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

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_status_subscription_keeps_the_connection_and_publishes_online()
    {
        var connection = new ScriptedConnection();
        connection.FailNextSubscribe(new MqttCommunicationException("simulated subscribe failure"));
        var transport = new ScriptedTransport();

        _ = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(1, connection.ConnectCalls),
            () => Assert.Equal(1, connection.SubscribeCalls),
            () => Assert.True(_health.Current.IsConnected));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failed_connect_that_also_signals_a_disconnect_schedules_one_reconnect()
    {
        var attempts = CreateTestProbe();
        var connection = new ScriptedConnection(attempts.Ref) { FailConnectCount = 2 };
        var transport = new ScriptedTransport();

        _ = CreateActor(connection, transport);

        attempts.ExpectMsg<ConnectAttempted>(m => m.Attempt == 1, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        attempts.ExpectMsg<ConnectAttempted>(m => m.Attempt == 2, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        attempts.ExpectMsg<ConnectAttempted>(m => m.Attempt == 3, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);
        attempts.ExpectNoMsg(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(3, connection.ConnectCalls),
            () => Assert.Equal(0, connection.RejectedWhileConnected));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_disconnect_timestamp_survives_failed_reconnect_attempts()
    {
        var attempts = CreateTestProbe();
        var connection = new ScriptedConnection(attempts.Ref);
        var transport = new ScriptedTransport();
        _ = CreateActor(connection, transport);
        attempts.ExpectMsg<ConnectAttempted>(TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        connection.FailNextConnects(3);
        connection.SimulateDisconnect();
        attempts.ExpectMsg<ConnectAttempted>(m => m.Attempt == 2, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(3));
        attempts.ExpectMsg<ConnectAttempted>(m => m.Attempt == 3, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);
        attempts.ExpectMsg<ConnectAttempted>(m => m.Attempt == 4, TestTimeouts.AwaitAssertMax, cancellationToken: TestContext.Current.CancellationToken);

        var mqtt = _health.Current;
        Assert.Multiple(
            () => Assert.False(mqtt.IsConnected),
            () => Assert.Equal(Epoch, mqtt.DisconnectedSince));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Stopping_publishes_offline()
    {
        var connection = new ScriptedConnection();
        var transport = new ScriptedTransport();
        var actor = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);

        Assert.True(await actor.GracefulStop(TestTimeouts.AwaitAssertMax));

        await transport.WaitForSent(sent => sent.Any(m => m is { Topic: AvailabilityTopic, Payload: "offline", Retain: true }))
            .WaitAsync(TestTimeouts.RealTimeWaitMax, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Stopping_completes_when_the_offline_publish_fails()
    {
        var connection = new ScriptedConnection();
        var transport = new ScriptedTransport();
        var actor = CreateActor(connection, transport);
        await WaitForOnline(transport, TestContext.Current.CancellationToken);
        transport.FailNextOffline(new MqttClientNotConnectedException());

        Assert.True(await actor.GracefulStop(TestTimeouts.AwaitAssertMax));
        Assert.Equal(1, transport.OfflineAttempts);
    }

    // --- Fakes modelled on MQTTnet 5 -------------------------------------

    private sealed record ConnectAttempted(int Attempt);

    // Mirrors MqttClient: a connect while connected is rejected, and a failed connect also raises the
    // disconnected event (MQTTnet raises DisconnectedAsync with ClientWasConnected = false).
    private sealed class ScriptedConnection(IActorRef? attempts = null) : IMqttConnection
    {
        private readonly object _lock = new();
        private readonly Queue<Exception> _subscribeFailures = new();
        private Action<string, string>? _onMessage;
        private Action? _onDisconnected;
        private bool _connected;
        private int _failConnects;
        private int _connectCalls;
        private int _subscribeCalls;
        private int _rejectedWhileConnected;

        public int FailConnectCount { init => _failConnects = value; }

        public int ConnectCalls { get { lock (_lock) { return _connectCalls; } } }

        public int SubscribeCalls { get { lock (_lock) { return _subscribeCalls; } } }

        public int RejectedWhileConnected { get { lock (_lock) { return _rejectedWhileConnected; } } }

        public void FailNextConnects(int count)
        {
            lock (_lock) { _failConnects = count; }
        }

        public void FailNextSubscribe(Exception cause)
        {
            lock (_lock) { _subscribeFailures.Enqueue(cause); }
        }

        public Task ConnectAsync(Action<string, string> onMessage, Action onDisconnected, CancellationToken cancellationToken)
        {
            int attempt;
            bool fail;
            lock (_lock)
            {
                attempt = ++_connectCalls;
                _onMessage = onMessage;
                _onDisconnected = onDisconnected;
                if (_connected)
                {
                    _rejectedWhileConnected++;
                    attempts?.Tell(new ConnectAttempted(attempt));
                    return Task.FromException(new InvalidOperationException(
                        "It is not allowed to connect with a server after the connection is established."));
                }

                fail = _failConnects > 0;
                if (fail)
                {
                    _failConnects--;
                }
                else
                {
                    _connected = true;
                }
            }

            attempts?.Tell(new ConnectAttempted(attempt));
            if (!fail)
            {
                return Task.CompletedTask;
            }

            onDisconnected();
            return Task.FromException(new MqttCommunicationException($"Simulated connect failure #{attempt}"));
        }

        public Task SubscribeAsync(string topicFilter, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                _subscribeCalls++;
                return _subscribeFailures.TryDequeue(out var cause) ? Task.FromException(cause) : Task.CompletedTask;
            }
        }

        public void SimulateDisconnect()
        {
            Action? onDisconnected;
            lock (_lock)
            {
                _connected = false;
                onDisconnected = _onDisconnected;
            }

            onDisconnected?.Invoke();
        }

        public void SimulateInbound(string topic, string payload)
        {
            Action<string, string>? onMessage;
            lock (_lock) { onMessage = _onMessage; }
            onMessage?.Invoke(topic, payload);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ScriptedTransport : IMqttTransport
    {
        private readonly object _lock = new();
        private readonly Queue<Func<CancellationToken, Task>> _onlineScript = new();
        private readonly Queue<Func<CancellationToken, Task>> _offlineScript = new();
        private readonly List<SentMessage> _sent = [];
        private readonly List<(Func<IReadOnlyList<SentMessage>, bool> Predicate, TaskCompletionSource Tcs)> _waiters = [];
        private int _onlineAttempts;
        private int _offlineAttempts;

        public IReadOnlyList<SentMessage> Sent { get { lock (_lock) { return [.. _sent]; } } }

        public int OnlineAttempts { get { lock (_lock) { return _onlineAttempts; } } }

        public int OfflineAttempts { get { lock (_lock) { return _offlineAttempts; } } }

        public void FailNextOnline(Exception cause)
        {
            lock (_lock) { _onlineScript.Enqueue(_ => Task.FromException(cause)); }
        }

        public void HangNextOnline()
        {
            lock (_lock) { _onlineScript.Enqueue(ct => new TaskCompletionSource().Task.WaitAsync(ct)); }
        }

        public void FailNextOffline(Exception cause)
        {
            lock (_lock) { _offlineScript.Enqueue(_ => Task.FromException(cause)); }
        }

        public Task SendAsync(string topic, string payload, bool retain, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task>? step = null;
            lock (_lock)
            {
                if (topic == AvailabilityTopic && payload == "online")
                {
                    _onlineAttempts++;
                    _onlineScript.TryDequeue(out step);
                }
                else if (topic == AvailabilityTopic && payload == "offline")
                {
                    _offlineAttempts++;
                    _offlineScript.TryDequeue(out step);
                }

                if (step is null)
                {
                    _sent.Add(new SentMessage(topic, payload, retain));
                    for (var i = _waiters.Count - 1; i >= 0; i--)
                    {
                        if (_waiters[i].Predicate(_sent))
                        {
                            _waiters[i].Tcs.TrySetResult();
                            _waiters.RemoveAt(i);
                        }
                    }
                }
            }

            return step is null ? Task.CompletedTask : step(cancellationToken);
        }

        public Task WaitForSent(Func<IReadOnlyList<SentMessage>, bool> predicate)
        {
            lock (_lock)
            {
                if (predicate(_sent))
                {
                    return Task.CompletedTask;
                }

                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add((predicate, tcs));
                return tcs.Task;
            }
        }
    }
}
