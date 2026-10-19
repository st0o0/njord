using Akka.Actor;
using MQTTnet.Exceptions;
using Njord.Mqtt.Transport;

namespace Njord.Mqtt.Tests;

internal sealed record ConnectAttempted(int Attempt);

// Mirrors MqttClient: a connect while connected is rejected, and a failed connect also raises the
// disconnected event (MQTTnet raises DisconnectedAsync with ClientWasConnected = false).
internal sealed class ScriptedMqttConnection(IActorRef? attempts = null) : IMqttConnection
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
