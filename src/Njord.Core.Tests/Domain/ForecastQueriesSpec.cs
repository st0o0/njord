using Njord.Core.Domain;
using Njord.Model;

namespace Njord.Core.Tests.Domain;

public sealed class ForecastQueriesSpec
{
    private static readonly DateTimeOffset T6 = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);

    private static DomainForecast<FloodPoint> HourlyFlood() =>
        new(DomainKind.Flood, T6, TimeResolution.Hourly,
            [new FloodPoint(T6, 1), new FloodPoint(T6.AddHours(1), 2), new FloodPoint(T6.AddHours(2), 3)]);

    private static DomainForecast<SolarPoint> HourlySolar(int hours) =>
        new(DomainKind.Solar, T6, TimeResolution.Hourly,
            [.. Enumerable.Range(0, hours).Select(h => new SolarPoint(T6.AddHours(h), h, 0, 0, 0))]);

    private static DomainForecast<FloodPoint> SixHourlyFlood() =>
        new(DomainKind.Flood, T6, TimeResolution.SixHourly,
            [new FloodPoint(T6, 10), new FloodPoint(T6.AddHours(6), 20)]);

    [Fact]
    public void Value_at_exact_time_returns_that_point()
    {
        var point = ForecastQueries.ValueAt(HourlyFlood(), T6.AddHours(1));

        Assert.NotNull(point);
        Assert.Equal(2, point.Discharge);
    }

    [Fact]
    public void Value_at_between_points_returns_the_point_whose_window_contains_time()
    {
        var point = ForecastQueries.ValueAt(HourlyFlood(), T6.AddHours(1).AddMinutes(30));

        Assert.NotNull(point);
        Assert.Equal(2, point.Discharge);
    }

    [Fact]
    public void Value_at_window_end_belongs_to_the_next_point()
    {
        var point = ForecastQueries.ValueAt(HourlyFlood(), T6.AddHours(2));

        Assert.NotNull(point);
        Assert.Equal(3, point.Discharge);
    }

    [Fact]
    public void Value_at_out_of_range_returns_null()
    {
        var forecast = HourlyFlood();

        Assert.Multiple(
            () => Assert.Null(ForecastQueries.ValueAt(forecast, T6.AddMinutes(-1))),
            () => Assert.Null(ForecastQueries.ValueAt(forecast, T6.AddHours(3))));
    }

    [Fact]
    public void Window_returns_points_in_inclusive_range()
    {
        var points = ForecastQueries.Window(HourlyFlood(), T6.AddHours(1), T6.AddHours(2));

        Assert.Equal([2.0, 3.0], points.Select(p => p.Discharge));
    }

    [Fact]
    public void Window_outside_the_series_is_empty()
    {
        Assert.Empty(ForecastQueries.Window(HourlyFlood(), T6.AddHours(10), T6.AddHours(12)));
    }

    [Fact]
    public void Nearest_returns_closest_point_within_tolerance()
    {
        var point = ForecastQueries.Nearest(HourlyFlood(), T6.AddMinutes(70), TimeSpan.FromMinutes(30));

        Assert.NotNull(point);
        Assert.Equal(2, point.Discharge);
    }

    [Fact]
    public void Nearest_beyond_tolerance_returns_null()
    {
        Assert.Null(ForecastQueries.Nearest(HourlyFlood(), T6.AddHours(5), TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void Correlate_same_resolution_pairs_matching_hours()
    {
        var pairs = ForecastQueries.Correlate(HourlyFlood(), HourlySolar(2));

        Assert.Equal(2, pairs.Count);
        Assert.Multiple(
            () => Assert.Equal((1.0, 0.0), (pairs[0].First.Discharge, pairs[0].Second.DirectRadiation)),
            () => Assert.Equal((2.0, 1.0), (pairs[1].First.Discharge, pairs[1].Second.DirectRadiation)));
    }

    [Fact]
    public void Correlate_different_resolution_pairs_each_coarse_point_with_hours_in_its_window()
    {
        var pairs = ForecastQueries.Correlate(SixHourlyFlood(), HourlySolar(12));

        Assert.Equal(12, pairs.Count);
        Assert.Multiple(
            () => Assert.Equal(6, pairs.Count(p => p.First.Discharge == 10)),
            () => Assert.Equal([0.0, 1.0, 2.0, 3.0, 4.0, 5.0],
                pairs.Where(p => p.First.Discharge == 10).Select(p => p.Second.DirectRadiation)),
            () => Assert.Equal([6.0, 7.0, 8.0, 9.0, 10.0, 11.0],
                pairs.Where(p => p.First.Discharge == 20).Select(p => p.Second.DirectRadiation)));
    }

    [Fact]
    public void Correlate_without_overlap_is_empty()
    {
        var later = new DomainForecast<SolarPoint>(
            DomainKind.Solar, T6, TimeResolution.Hourly, [new SolarPoint(T6.AddHours(20), 1, 0, 0, 0)]);

        Assert.Empty(ForecastQueries.Correlate(HourlyFlood(), later));
    }
}
