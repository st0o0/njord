using Njord.Core.Domain;
using Njord.Model;

namespace Njord.Core.Tests.Domain;

public sealed class LocationForecastSpec
{
    private static readonly DateTimeOffset Run = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Location Lucerne = new("Lucerne", 47.05, 8.31);

    private static DomainForecast<FloodPoint> Flood(double discharge) =>
        new(DomainKind.Flood, Run, TimeResolution.Hourly, [new FloodPoint(Run, discharge)]);

    private static DomainForecast<WeatherPoint> Weather(double temperature) =>
        new(DomainKind.Weather, Run, TimeResolution.Hourly,
            [new WeatherPoint(Run, temperature, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)]);

    [Fact]
    public void New_forecast_has_no_domains()
    {
        var forecast = new LocationForecast(Lucerne);

        Assert.Multiple(
            () => Assert.Equal(Lucerne, forecast.Location),
            () => Assert.Null(forecast.Weather),
            () => Assert.Null(forecast.AirQuality),
            () => Assert.Null(forecast.Flood),
            () => Assert.Null(forecast.Solar),
            () => Assert.Null(forecast.Marine));
    }

    [Fact]
    public void With_updates_one_domain_and_preserves_others()
    {
        var flood = Flood(5);
        var original = new LocationForecast(Lucerne, Weather(10), Flood: flood);
        var newWeather = Weather(20);

        var updated = original.With(DomainKind.Weather, newWeather);
        var originalWeather = original.Weather;
        Assert.NotNull(originalWeather);

        Assert.Multiple(
            () => Assert.Same(newWeather, updated.Weather),
            () => Assert.Same(flood, updated.Flood),
            () => Assert.Equal(10, originalWeather.Points[0].Temperature));
    }

    [Fact]
    public void With_sets_each_domain()
    {
        var air = new DomainForecast<AirQualityPoint>(
            DomainKind.AirQuality, Run, TimeResolution.Hourly, [new AirQualityPoint(Run, 1, 2, 3)]);
        var solar = new DomainForecast<SolarPoint>(
            DomainKind.Solar, Run, TimeResolution.Hourly, [new SolarPoint(Run, 1, 2, 3, 4)]);
        var marine = new DomainForecast<MarinePoint>(
            DomainKind.Marine, Run, TimeResolution.Hourly, [new MarinePoint(Run, 1, 2, 3)]);

        var forecast = new LocationForecast(Lucerne)
            .With(DomainKind.AirQuality, air)
            .With(DomainKind.Solar, solar)
            .With(DomainKind.Marine, marine);

        Assert.Multiple(
            () => Assert.Same(air, forecast.AirQuality),
            () => Assert.Same(solar, forecast.Solar),
            () => Assert.Same(marine, forecast.Marine),
            () => Assert.Null(forecast.Weather),
            () => Assert.Null(forecast.Flood));
    }

    [Fact]
    public void With_rejects_a_forecast_that_does_not_match_the_domain()
    {
        var forecast = new LocationForecast(Lucerne);

        var ex = Assert.Throws<ArgumentException>(() => forecast.With(DomainKind.Weather, Flood(1)));

        Assert.Equal("forecast", ex.ParamName);
    }
}
