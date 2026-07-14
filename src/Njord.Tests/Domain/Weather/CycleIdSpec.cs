using Njord.Domain.Weather;

namespace Njord.Tests.Domain.Weather;

public sealed class CycleIdSpec
{
    private static readonly DateTimeOffset Ts = new(2026, 7, 12, 14, 30, 0, TimeSpan.Zero);

    [Fact(Timeout = 5000)]
    public void Timestamp_is_preserved()
    {
        var cycle = new CycleId(Ts);

        Assert.Equal(Ts, cycle.Timestamp);
    }

    [Fact(Timeout = 5000)]
    public void Equal_timestamps_produce_equal_cycle_ids()
    {
        var a = new CycleId(Ts);
        var b = new CycleId(Ts);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact(Timeout = 5000)]
    public void Different_timestamps_produce_different_cycle_ids()
    {
        var a = new CycleId(Ts);
        var b = new CycleId(Ts.AddMinutes(1));

        Assert.NotEqual(a, b);
    }

    [Fact(Timeout = 5000)]
    public void ToString_returns_round_trip_format()
    {
        var cycle = new CycleId(Ts);

        Assert.Equal(Ts.ToString("O"), cycle.ToString());
    }
}
