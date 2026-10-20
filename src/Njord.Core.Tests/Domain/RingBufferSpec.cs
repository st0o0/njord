using Njord.Core.Domain;

namespace Njord.Core.Tests.Domain;

public sealed class RingBufferSpec
{
    [Fact]
    public void Empty_buffer_has_zero_count()
    {
        var ring = new RingBuffer<int>(4);

        Assert.Equal(0, ring.Count);
    }

    [Fact]
    public void Push_increments_count()
    {
        var ring = new RingBuffer<int>(4);

        ring.Push(1);
        ring.Push(2);

        Assert.Equal(2, ring.Count);
    }

    [Fact]
    public void Index_zero_returns_most_recent()
    {
        var ring = new RingBuffer<int>(4);

        ring.Push(1);
        ring.Push(2);
        ring.Push(3);

        Assert.Equal(3, ring[0]);
    }

    [Fact]
    public void Index_returns_items_most_recent_first()
    {
        var ring = new RingBuffer<int>(4);

        ring.Push(10);
        ring.Push(20);
        ring.Push(30);

        Assert.Multiple(
            () => Assert.Equal(30, ring[0]),
            () => Assert.Equal(20, ring[1]),
            () => Assert.Equal(10, ring[2]));
    }

    [Fact]
    public void Count_does_not_exceed_capacity()
    {
        var ring = new RingBuffer<int>(2);

        ring.Push(1);
        ring.Push(2);
        ring.Push(3);

        Assert.Equal(2, ring.Count);
    }

    [Fact]
    public void Oldest_item_is_evicted_when_full()
    {
        var ring = new RingBuffer<int>(2);

        ring.Push(1);
        ring.Push(2);
        ring.Push(3);

        Assert.Multiple(
            () => Assert.Equal(3, ring[0]),
            () => Assert.Equal(2, ring[1]));
    }

    [Fact]
    public void Index_out_of_range_throws()
    {
        var ring = new RingBuffer<int>(4);
        ring.Push(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => ring[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => ring[-1]);
    }

    [Fact]
    public void Zero_capacity_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(0));
    }
}
