using Njord.Mqtt.Transport;

namespace Njord.Mqtt.Tests;

internal sealed class ScriptedPublishTransport : IMqttTransport
{
    private readonly Queue<Func<CancellationToken, Task>> _script = new();
    private readonly List<SentMessage> _sent = [];
    private readonly List<(Func<int, bool> Predicate, TaskCompletionSource Tcs)> _attemptWaiters = [];
    private readonly List<(Func<SentMessage, bool> Predicate, TaskCompletionSource Tcs)> _sentWaiters = [];
    private readonly object _lock = new();
    private int _attempts;

    public int Attempts { get { lock (_lock) { return _attempts; } } }

    public IReadOnlyList<SentMessage> Sent { get { lock (_lock) { return [.. _sent]; } } }

    public void FailNext(Exception cause)
    {
        lock (_lock) { _script.Enqueue(_ => Task.FromException(cause)); }
    }

    public void HangNext()
    {
        lock (_lock) { _script.Enqueue(ct => new TaskCompletionSource().Task.WaitAsync(ct)); }
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
                var msg = new SentMessage(topic, payload, retain);
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

    public Task WaitForSent(Func<SentMessage, bool> predicate)
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
