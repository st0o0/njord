using Njord.Domain.Weather;

namespace Njord.Tests.Domain.Weather;

public sealed class ForecastSeriesQuerySpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temp = ParameterRegistry.Temperature2m;

    private static ForecastSeries MakeSeries(params (int hourOffset, double? value)[] points)
    {
        return new ForecastSeries(points.Select(p =>
            new ForecastPoint(T0.AddHours(p.hourOffset), new Dictionary<ParameterDef, double?>
            {
                [Temp] = p.value,
            })));
    }

    [Fact(Timeout = 5000)]
    public void Window_filters_points_by_inclusive_range()
    {
        var series = MakeSeries((0, 10), (2, 12), (4, 14), (6, 16));

        var windowed = series.Window(T0.AddHours(2), T0.AddHours(4));

        Assert.Equal(2, windowed.Points.Count);
        Assert.Equal(T0.AddHours(2), windowed.Points[0].ValidAt);
        Assert.Equal(T0.AddHours(4), windowed.Points[1].ValidAt);
    }

    [Fact(Timeout = 5000)]
    public void Window_returns_empty_when_no_match()
    {
        var series = MakeSeries((0, 10), (2, 12));

        var windowed = series.Window(T0.AddHours(10), T0.AddHours(12));

        Assert.Empty(windowed.Points);
    }

    [Fact(Timeout = 5000)]
    public void Mean_computes_average_of_non_null_values()
    {
        var series = MakeSeries((0, 20.0), (2, 22.0), (4, 24.0));

        var mean = series.Mean(Temp, T0, T0.AddHours(4));

        Assert.Equal(22.0, mean);
    }

    [Fact(Timeout = 5000)]
    public void Mean_skips_null_values()
    {
        var series = MakeSeries((0, 20.0), (2, null), (4, 24.0));

        var mean = series.Mean(Temp, T0, T0.AddHours(4));

        Assert.Equal(22.0, mean);
    }

    [Fact(Timeout = 5000)]
    public void Mean_returns_null_when_all_values_are_null()
    {
        var series = MakeSeries((0, null), (2, null));

        var mean = series.Mean(Temp, T0, T0.AddHours(4));

        Assert.Null(mean);
    }

    [Fact(Timeout = 5000)]
    public void Values_returns_non_null_values_only()
    {
        var series = MakeSeries((0, 5.0), (2, null), (4, 7.0));

        var values = series.Values(Temp, T0, T0.AddHours(4)).ToList();

        Assert.Equal([5.0, 7.0], values);
    }
}
