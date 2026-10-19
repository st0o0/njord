using Njord.Mqtt.Transport;

namespace Njord.Mqtt.Tests;

internal sealed record SentMessage(string Topic, string Payload, bool Retain);

internal sealed class ScriptedAvailabilityTransport : IMqttTransport
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
            if (topic == MqttConnectionSpecBase.AvailabilityTopic && payload == "online")
            {
                _onlineAttempts++;
                _onlineScript.TryDequeue(out step);
            }
            else if (topic == MqttConnectionSpecBase.AvailabilityTopic && payload == "offline")
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
