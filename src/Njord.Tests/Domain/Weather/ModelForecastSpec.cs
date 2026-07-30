using Njord.Domain.Weather;

namespace Njord.Tests.Domain.Weather;

public sealed class ModelForecastSpec
{
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;

    [Fact(Timeout = 5000)]
    public void A_forecast_is_fully_attributable()
    {
        var cycle = new CycleId(new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero));
        var point = new ForecastPoint(cycle.Timestamp.AddHours(3), new Dictionary<ParameterDef, double?> { [Temperature] = 20.0 });
        var series = new ForecastSeries([point]);

        var forecast = new ModelForecast(new WeatherModel("ecmwf_ifs025"), "home", cycle, series, DailyForecastSeries.Empty);

        Assert.Equal("ecmwf_ifs025", forecast.Model.Id);
        Assert.Equal("home", forecast.Location);
        Assert.Equal(cycle, forecast.Cycle);
        Assert.Single(forecast.Hourly.Points);
    }
}
