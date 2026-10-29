using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Messages.Stream;

namespace Njord.Tests.Shared;

public static class DomainData
{
    public static readonly Location Lucerne = new("Lucerne", 47.05, 8.31);
    public static readonly Location Bern = new("Bern", 46.95, 7.45);

    public static ForecastPoint BenignPoint(DateTimeOffset validFrom) => new(
        validFrom,
        Temperature: 10.0,
        ApparentTemperature: 10.0,
        WindSpeed: 2.0,
        Precipitation: 0.0,
        PrecipitationProbability: 0.0,
        CloudCover: 50.0,
        Pressure: 1013.0,
        WeatherCode: 0,
        Humidity: 60.0,
        DewPoint: 5.0,
        UvIndex: 0.0,

        WindGusts: 4.0,
        Visibility: 20000.0,
        Cape: 0.0,
        Rain: 0.0,
        Snowfall: 0.0,
        FreezingLevelHeight: 2500.0,
        IsDay: 1,
        SoilTemperature: 10.0);

    public static DomainForecast<ForecastPoint> Forecast(
        DateTimeOffset start, int hours = 48, Func<int, ForecastPoint, ForecastPoint>? shape = null)
    {
        var points = Enumerable.Range(0, hours).Select(hour =>
        {
            var point = BenignPoint(start.AddHours(hour));
            return shape?.Invoke(hour, point) ?? point;
        });
        return new DomainForecast<ForecastPoint>(DomainKind.Forecast, start, TimeResolution.Hourly, [.. points]);
    }

    public static DomainForecast<FloodPoint> Flood(DateTimeOffset start, double discharge = 12.5) =>
        new(DomainKind.Flood, start, TimeResolution.Daily, [new FloodPoint(start, discharge)]);

    public static DomainDataReceived Received(Location location, DomainForecast<ForecastPoint> forecast) =>
        new(location, DomainKind.Forecast, forecast);

    public static DomainDataReceived Received(Location location, DomainForecast<FloodPoint> forecast) =>
        new(location, DomainKind.Flood, forecast);

    public static DomainForecast<EnsemblePoint> Ensemble(
        DateTimeOffset start, int hours = 48, double temperatureSpread = 1.0) =>
        new(DomainKind.Ensemble, start, TimeResolution.Hourly,
            [.. Enumerable.Range(0, hours).Select(h =>
                new EnsemblePoint(start.AddHours(h), temperatureSpread, 0.3, 0.5))]);

    public static DomainDataReceived Received(Location location, DomainForecast<EnsemblePoint> forecast) =>
        new(location, DomainKind.Ensemble, forecast);
}
