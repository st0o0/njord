using Microsoft.Extensions.Time.Testing;
using Njord.Domain;

namespace Njord.Tests.Domain;

public sealed class ForecastDataHashSpec
{
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef Humidity = ParameterRegistry.GetByApiName("relative_humidity_2m")!;
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 14, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(Now);

    private static readonly DateTimeOffset Tomorrow = Now.Date.AddDays(1);

    private static ForecastPoint HourlyPoint(DateTimeOffset validAt, double temp, double humidity) =>
        new(validAt, new Dictionary<ParameterDef, double?>
        {
            [Temperature] = temp,
            [Humidity] = humidity,
        });

    private static ModelForecast Forecast(IEnumerable<ForecastPoint> points) =>
        new(new WeatherModel("icon_d2"), "lucerne", new CycleId(Now),
            new ForecastSeries(points), DailyForecastSeries.Empty);

    [Fact(Timeout = 5000)]
    public void Same_data_produces_same_hash()
    {
        var points = new[]
        {
            HourlyPoint(Tomorrow, 20.0, 65),
            HourlyPoint(Tomorrow.AddHours(3), 22.0, 60),
        };
        var a = ForecastDataHash.Compute(Forecast(points), Time);
        var b = ForecastDataHash.Compute(Forecast(points), Time);
        Assert.Equal(a, b);
    }

    [Fact(Timeout = 5000)]
    public void Different_values_produce_different_hash()
    {
        var a = ForecastDataHash.Compute(Forecast([
            HourlyPoint(Tomorrow, 20.0, 65),
        ]), Time);
        var b = ForecastDataHash.Compute(Forecast([
            HourlyPoint(Tomorrow, 21.0, 65),
        ]), Time);
        Assert.NotEqual(a, b);
    }

    [Fact(Timeout = 5000)]
    public void Timestamp_only_change_produces_same_hash()
    {
        var a = ForecastDataHash.Compute(
            new ModelForecast(new WeatherModel("icon_d2"), "lucerne", new CycleId(Now),
                new ForecastSeries([HourlyPoint(Tomorrow, 20.0, 65)]), DailyForecastSeries.Empty),
            Time);
        var b = ForecastDataHash.Compute(
            new ModelForecast(new WeatherModel("icon_d2"), "lucerne", new CycleId(Now.AddMinutes(20)),
                new ForecastSeries([HourlyPoint(Tomorrow, 20.0, 65)]), DailyForecastSeries.Empty),
            Time);
        Assert.Equal(a, b);
    }

    [Fact(Timeout = 5000)]
    public void Cutoff_excludes_todays_points()
    {
        var todayPoint = HourlyPoint(Now.AddHours(1), 99.0, 99);
        var tomorrowPoint = HourlyPoint(Tomorrow, 20.0, 65);

        var withToday = ForecastDataHash.Compute(Forecast([todayPoint, tomorrowPoint]), Time);
        var withoutToday = ForecastDataHash.Compute(Forecast([tomorrowPoint]), Time);

        Assert.Equal(withToday, withoutToday);
    }

    [Fact(Timeout = 5000)]
    public void Null_values_are_hashed_consistently()
    {
        var pointWithNull = new ForecastPoint(Tomorrow, new Dictionary<ParameterDef, double?>
        {
            [Temperature] = null,
            [Humidity] = 65,
        });
        var a = ForecastDataHash.Compute(Forecast([pointWithNull]), Time);
        var b = ForecastDataHash.Compute(Forecast([pointWithNull]), Time);
        Assert.Equal(a, b);
    }

    [Fact(Timeout = 5000)]
    public void Null_value_differs_from_zero_value()
    {
        var withNull = new ForecastPoint(Tomorrow, new Dictionary<ParameterDef, double?>
        {
            [Temperature] = null,
        });
        var withZero = new ForecastPoint(Tomorrow, new Dictionary<ParameterDef, double?>
        {
            [Temperature] = 0.0,
        });
        var a = ForecastDataHash.Compute(Forecast([withNull]), Time);
        var b = ForecastDataHash.Compute(Forecast([withZero]), Time);
        Assert.NotEqual(a, b);
    }
}
