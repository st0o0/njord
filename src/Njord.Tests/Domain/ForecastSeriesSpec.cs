using Njord.Domain;

namespace Njord.Tests.Domain;

public sealed class ForecastSeriesSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef Dewpoint = ParameterRegistry.GetByApiName("dew_point_2m")!;

    private static ForecastPoint Point(DateTimeOffset validAt, double? temp = null, double? dew = null)
    {
        var values = new Dictionary<ParameterDef, double?>();
        if (temp is not null)
        {
            values[Temperature] = temp;
        }

        if (dew is not null)
        {
            values[Dewpoint] = dew;
        }

        return new ForecastPoint(validAt, values);
    }

    [Fact(Timeout = 5000)]
    public void Unordered_points_are_normalized_to_ascending_valid_at()
    {
        var series = new ForecastSeries([
            Point(T0.AddHours(6), temp: 21.0),
            Point(T0, temp: 19.0),
            Point(T0.AddHours(3), temp: 20.0),
        ]);

        Assert.Equal([T0, T0.AddHours(3), T0.AddHours(6)], series.Points.Select(p => p.ValidAt));
    }

    [Fact(Timeout = 5000)]
    public void A_point_with_a_missing_parameter_is_retained()
    {
        var point = Point(T0, temp: 19.5, dew: null);

        var series = new ForecastSeries([point]);

        Assert.Single(series.Points);
        Assert.Equal(19.5, series.Points[0].Get(Temperature));
        Assert.Null(series.Points[0].Get(Dewpoint));
    }

    [Fact(Timeout = 5000)]
    public void All_parameters_in_values_are_readable_through_the_accessor()
    {
        var values = new Dictionary<ParameterDef, double?> { [Temperature] = 18.5, [Dewpoint] = 12.3 };
        var point = new ForecastPoint(T0, values);

        Assert.Equal(18.5, point.Get(Temperature));
        Assert.Equal(12.3, point.Get(Dewpoint));
    }
}
