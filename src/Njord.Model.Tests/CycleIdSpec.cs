namespace Njord.Model.Tests;

public sealed class CycleIdSpec
{
    [Fact]
    public void ToString_returns_round_trip_format()
    {
        var cycle = new CycleId(new DateTimeOffset(2026, 7, 12, 14, 30, 0, TimeSpan.Zero));

        Assert.Equal("2026-07-12T14:30:00.0000000+00:00", cycle.ToString());
    }
}
