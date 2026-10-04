using Microsoft.Extensions.Options;

namespace Njord.Tests.Shared;

public sealed class TestOptionsMonitor<T>(T initial) : IOptionsMonitor<T>
{
    private readonly List<Action<T, string?>> _listeners = [];

    public T CurrentValue { get; private set; } = initial;

    public T Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<T, string?> listener)
    {
        _listeners.Add(listener);
        return new Unsubscribe(() => _listeners.Remove(listener));
    }

    public void Update(T newValue)
    {
        CurrentValue = newValue;
        foreach (var listener in _listeners)
        {
            listener(newValue, Options.DefaultName);
        }
    }

    private sealed class Unsubscribe(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
