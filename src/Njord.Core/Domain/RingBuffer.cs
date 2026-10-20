namespace Njord.Core.Domain;

public sealed class RingBuffer<T>(int capacity)
{
    private readonly T[] _buffer = new T[capacity];
    private int _head;

    public int Capacity { get; } = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    public int Count { get; private set; }

    public void Push(T item)
    {
        _head = (_head - 1 + Capacity) % Capacity;
        _buffer[_head] = item;
        if (Count < Capacity)
        {
            Count++;
        }
    }

    public void ReplaceNewest(T item)
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("The ring is empty.");
        }

        _buffer[_head] = item;
    }

    public T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            return _buffer[(_head + index) % Capacity];
        }
    }
}
