using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Flood;

namespace Njord.Core.Tests.Domain;

public sealed class ForecastWindowSpec
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
        var points = ForecastWindow.NextHours(Hourly(48), Midnight.AddMinutes(30), 6);

        Assert.Equal(6, points.Count);
        Assert.Equal(Midnight, points[0].ValidFrom);
    }

    [Fact]
    public void Should_default_to_24_hours_half_open()
    {
        Assert.Equal(24, ForecastWindow.NextHours(Hourly(48), Midnight).Count);
    }

    [Fact]
    public void Should_include_the_end_boundary_when_inclusive()
    {
        Assert.Equal(25, ForecastWindow.NextHoursInclusive(Hourly(48), Midnight).Count);
    }

    [Fact]
    public void Should_anchor_days_to_utc_midnight()
    {
        var points = ForecastWindow.NextDays(Daily(10), Midnight.AddHours(15), 3);

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
}
