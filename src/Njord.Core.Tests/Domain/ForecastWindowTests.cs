using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Core.Forecast;

namespace Njord.Core.Tests.Domain;

public sealed class ForecastWindowTests
{
    private static readonly DateTimeOffset Midnight = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static DomainForecast<FloodPoint> Hourly(int count) =>
        new(DomainKind.Flood, Midnight, TimeResolution.Hourly,
            [.. Enumerable.Range(0, count).Select(h => new FloodPoint(Midnight.AddHours(h), h * 10.0))]);

    private static DomainForecast<FloodPoint> Daily(int count) =>
        new(DomainKind.Flood, Midnight, TimeResolution.Daily,
            [.. Enumerable.Range(0, count).Select(d => new FloodPoint(Midnight.AddDays(d), d * 10.0))]);

    [Fact]
    public void Should_return_exactly_the_requested_number_of_hours()
    {
        var points = Hourly(48).NextHours(Midnight.AddMinutes(30), 6);

        Assert.Equal(6, points.Count);
        Assert.Equal(Midnight, points[0].ValidFrom);
    }

    [Fact]
    public void Should_default_to_24_hours_half_open()
    {
        Assert.Equal(24, Hourly(48).NextHours(Midnight).Count);
    }

    [Fact]
    public void Should_include_the_end_boundary_when_inclusive()
    {
        Assert.Equal(25, Hourly(48).NextHoursInclusive(Midnight).Count);
    }

    [Fact]
    public void Should_anchor_days_to_utc_midnight()
    {
        var points = Daily(10).NextDays(Midnight.AddHours(15), 3);

        Assert.Equal(4, points.Count);
        Assert.Equal(Midnight, points[0].ValidFrom);
    }

    [Fact]
    public void Should_clamp_hours_from_to_zero_for_past_points()
    {
        var point = new FloodPoint(Midnight.AddHours(-5), 1.0);

        Assert.Equal(0, point.HoursFrom(Midnight));
    }

    [Fact]
    public void Should_anchor_hours_from_to_the_hour_boundary()
    {
        var point = new FloodPoint(Midnight.AddHours(3), 1.0);

        Assert.Equal(3, point.HoursFrom(Midnight.AddMinutes(45)));
    }

    [Fact]
    public void Should_truncate_to_the_start_of_the_hour()
    {
        var now = new DateTimeOffset(2026, 7, 1, 14, 37, 12, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero), now.HourStart());
    }

    [Fact]
    public void Should_normalize_other_offsets_to_utc_for_the_hour_start()
    {
        var now = new DateTimeOffset(2026, 7, 1, 16, 37, 0, TimeSpan.FromHours(2));

        var start = now.HourStart();

        Assert.Multiple(
            () => Assert.Equal(new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero), start),
            () => Assert.Equal(TimeSpan.Zero, start.Offset));
    }
}
