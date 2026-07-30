using Njord.Domain.Weather;

namespace Njord.Tests.Domain.Weather;

public sealed class TimeAnchorSpec
{
    [Fact(Timeout = 5000)]
    public void At_horizon_floors_to_current_hour_when_tick_is_not_on_the_hour()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 14, 15, 0, TimeSpan.Zero);

        var result = TimeAnchor.AtHorizon(tick, 3);

        Assert.Equal(new DateTimeOffset(2026, 7, 12, 17, 0, 0, TimeSpan.Zero), result);
    }

    [Fact(Timeout = 5000)]
    public void At_horizon_preserves_exact_hour_when_tick_is_on_the_hour()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 14, 0, 0, TimeSpan.Zero);

        var result = TimeAnchor.AtHorizon(tick, 3);

        Assert.Equal(new DateTimeOffset(2026, 7, 12, 17, 0, 0, TimeSpan.Zero), result);
    }

    [Fact(Timeout = 5000)]
    public void At_horizon_with_zero_offset_floors_to_current_hour()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 14, 30, 0, TimeSpan.Zero);

        var result = TimeAnchor.AtHorizon(tick, 0);

        Assert.Equal(new DateTimeOffset(2026, 7, 12, 14, 0, 0, TimeSpan.Zero), result);
    }

    [Fact(Timeout = 5000)]
    public void At_horizon_crosses_day_boundary()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 22, 0, 0, TimeSpan.Zero);

        var result = TimeAnchor.AtHorizon(tick, 6);

        Assert.Equal(new DateTimeOffset(2026, 7, 13, 4, 0, 0, TimeSpan.Zero), result);
    }

    [Fact(Timeout = 5000)]
    public void At_horizon_h0_never_returns_future_hour()
    {
        var tick = new DateTimeOffset(2026, 7, 12, 14, 59, 59, TimeSpan.Zero);

        var result = TimeAnchor.AtHorizon(tick, 0);

        Assert.Equal(new DateTimeOffset(2026, 7, 12, 14, 0, 0, TimeSpan.Zero), result);
    }
}
