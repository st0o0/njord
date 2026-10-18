using Njord.Mqtt.Transport;

namespace Njord.Mqtt.Tests;

internal sealed record SentMessage(string Topic, string Payload, bool Retain);

internal sealed class RecordingTransport : IMqttTransport
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
