using System.Collections.Immutable;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Flood;
using Njord.Forecast.Weather;

namespace Njord.Core.Tests.Domain;

public sealed class ForecastQueriesTests
{
    private static readonly DateTimeOffset Midnight = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static DomainForecast<FloodPoint> Hourly(params int[] hours) =>
        new(DomainKind.Flood, Midnight, TimeResolution.Hourly,
            [.. hours.Select(h => new FloodPoint(Midnight.AddHours(h), h * 10.0))]);

    [Fact]
    public void Should_find_the_point_whose_validity_slot_contains_the_time()
    {
        var forecast = Hourly(0, 1, 2);

        var point = ForecastQueries.ValueAt(forecast, Midnight.AddMinutes(90));

        Assert.NotNull(point);
        Assert.Equal(10.0, point.Discharge);
    }

    [Fact]
    public void Should_return_null_when_the_time_is_past_the_last_slot()
    {
        Assert.Null(ForecastQueries.ValueAt(Hourly(0, 1, 2), Midnight.AddHours(3)));
    }

    [Fact]
    public void Should_return_points_inside_the_window_inclusive_of_both_ends()
    {
        var window = ForecastQueries.Window(Hourly(0, 1, 2, 3, 4), Midnight.AddHours(1), Midnight.AddHours(3));

        Assert.Equal([10.0, 20.0, 30.0], window.Select(p => p.Discharge));
    }

    [Fact]
    public void Should_return_the_nearest_point_within_the_allowed_gap()
    {
        var forecast = Hourly(0, 3);

        var point = ForecastQueries.Nearest(forecast, Midnight.AddHours(2), TimeSpan.FromHours(1));

        Assert.NotNull(point);
        Assert.Equal(30.0, point.Discharge);
    }

    [Fact]
    public void Should_return_null_when_no_point_is_within_the_allowed_gap()
    {
        Assert.Null(ForecastQueries.Nearest(Hourly(0, 6), Midnight.AddHours(3), TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Should_correlate_hourly_points_with_the_daily_point_covering_them()
    {
        var hourly = new DomainForecast<WeatherPoint>(
            DomainKind.Weather, Midnight, TimeResolution.Hourly,
            [.. new[] { 0, 12, 24 }.Select(h =>
                new WeatherPoint(Midnight.AddHours(h), h, h, 0, 0, 0, 0, 1013, 0, 50, 0, 0))]);
        var daily = new DomainForecast<FloodPoint>(
            DomainKind.Flood, Midnight, TimeResolution.Daily,
            ImmutableArray.Create(new FloodPoint(Midnight, 99)));

        var pairs = ForecastQueries.Correlate(hourly, daily);

        Assert.Equal([0.0, 12.0], pairs.Select(p => p.First.Temperature));
    }
}
