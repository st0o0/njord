using System.Collections.Immutable;

namespace Njord.Core.Configuration;

// Fan-out of "configuration changed" signals to request handlers such as the admin StreamConfig RPC.
public sealed class ConfigChangeFeed
{
    private ImmutableList<Action> _subscribers = [];

    public IDisposable Subscribe(Action onChanged)
    {
        ImmutableInterlocked.Update(ref _subscribers, list => list.Add(onChanged));
        return new Subscription(() => ImmutableInterlocked.Update(ref _subscribers, list => list.Remove(onChanged)));
    }

    public void Publish()
    {
        foreach (var subscriber in Volatile.Read(ref _subscribers))
        {
            subscriber();
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
