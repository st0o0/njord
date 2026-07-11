using Njord.Domain;

namespace Njord.Tests.Domain;

public sealed class ModelForecastSpec
{
    [Fact(Timeout = 5000)]
    public void A_forecast_is_fully_attributable()
    {
        var cycle = new CycleId(new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero));
        var retrievedAt = cycle.Timestamp.AddSeconds(4);
        var series = new ForecastSeries([new ForecastPoint(cycle.Timestamp.AddHours(3), Temperature: 20.0)]);

        var forecast = new ModelForecast(new WeatherModel("ECMWF"), "home", cycle, retrievedAt, series);

        Assert.Equal("ECMWF", forecast.Model.Id);
        Assert.Equal("home", forecast.Location);
        Assert.Equal(cycle, forecast.Cycle);
        Assert.Equal(retrievedAt, forecast.RetrievedAt);
        Assert.Single(forecast.Series.Points);
    }
}
