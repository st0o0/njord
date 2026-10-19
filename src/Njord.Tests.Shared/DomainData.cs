using Njord.Core.Domain;
using Njord.Model;

namespace Njord.Tests.Shared;

public static class DomainData
{
    public static readonly Location Lucerne = new("Lucerne", 47.05, 8.31);
    public static readonly Location Bern = new("Bern", 46.95, 7.45);

    public static WeatherPoint BenignPoint(DateTimeOffset validFrom) => new(
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
        EnsembleStdDev: 0.5);

    public static DomainForecast<WeatherPoint> Weather(
        DateTimeOffset start, int hours = 48, Func<int, WeatherPoint, WeatherPoint>? shape = null)
    {
        var points = Enumerable.Range(0, hours).Select(hour =>
        {
            var point = BenignPoint(start.AddHours(hour));
            return shape?.Invoke(hour, point) ?? point;
        });
        return new DomainForecast<WeatherPoint>(DomainKind.Weather, start, TimeResolution.Hourly, [.. points]);
    }

    public static DomainForecast<FloodPoint> Flood(DateTimeOffset start, double discharge = 12.5) =>
        new(DomainKind.Flood, start, TimeResolution.Daily, [new FloodPoint(start, discharge)]);

    public static DomainDataReceived Received(Location location, DomainForecast<WeatherPoint> forecast) =>
        new(location, DomainKind.Weather, forecast);

    public static DomainDataReceived Received(Location location, DomainForecast<FloodPoint> forecast) =>
        new(location, DomainKind.Flood, forecast);
}
