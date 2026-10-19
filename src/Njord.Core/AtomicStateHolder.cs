namespace Njord.Core;

public sealed class AtomicStateHolder<T> where T : class
{
    private T? _state;

    public T? Current => Volatile.Read(ref _state);

    public void Update(T state) => Volatile.Write(ref _state, state);
}
