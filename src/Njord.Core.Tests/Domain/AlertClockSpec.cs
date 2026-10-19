using Njord.Core.Domain;

namespace Njord.Core.Tests.Domain;

public sealed class AlertClockSpec
{
    [Fact]
    public void Should_truncate_to_the_start_of_the_hour()
    {
        var now = new DateTimeOffset(2026, 7, 1, 14, 37, 12, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero), AlertClock.HourStart(now));
    }

    [Fact]
    public void Should_normalize_other_offsets_to_utc()
    {
        var now = new DateTimeOffset(2026, 7, 1, 16, 37, 0, TimeSpan.FromHours(2));

        var start = AlertClock.HourStart(now);

        Assert.Multiple(
            () => Assert.Equal(new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero), start),
            () => Assert.Equal(TimeSpan.Zero, start.Offset));
    }
}
