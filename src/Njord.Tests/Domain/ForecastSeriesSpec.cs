using Njord.Domain;

namespace Njord.Tests.Domain;

public sealed class ForecastSeriesSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact(Timeout = 5000)]
    public void Unordered_points_are_normalized_to_ascending_valid_at()
    {
        var series = new ForecastSeries([
            new ForecastPoint(T0.AddHours(6), Temperature: 21.0),
            new ForecastPoint(T0, Temperature: 19.0),
            new ForecastPoint(T0.AddHours(3), Temperature: 20.0),
        ]);

        Assert.Equal([T0, T0.AddHours(3), T0.AddHours(6)], series.Points.Select(p => p.ValidAt));
    }

    [Fact(Timeout = 5000)]
    public void A_point_with_a_missing_parameter_is_retained()
    {
        var point = new ForecastPoint(T0, Temperature: 19.5, Dewpoint: null);

        var series = new ForecastSeries([point]);

        Assert.Single(series.Points);
        Assert.Equal(19.5, series.Points[0].Get(WeatherParameter.Temperature));
        Assert.Null(series.Points[0].Get(WeatherParameter.Dewpoint));
    }

    [Fact(Timeout = 5000)]
    public void Every_parameter_is_readable_through_the_accessor()
    {
        var point = new ForecastPoint(
            T0,
            Temperature: 1, ApparentTemperature: 2, Precipitation: 3, WindSpeed: 4,
            WindGust: 5, Dewpoint: 6, RelativeHumidity: 7, CloudCover: 8, PressureMsl: 9);

        var values = Enum.GetValues<WeatherParameter>().Select(p => point.Get(p)).ToList();

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9], values.Select(v => v!.Value));
    }
}
