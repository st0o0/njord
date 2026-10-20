using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Weather;

namespace Njord.Tests.Shared;

public sealed class LocationForecastBuilder
{
    private LocationForecast _forecast;

    private LocationForecastBuilder(Location location) => _forecast = new LocationForecast(location);

    public static LocationForecastBuilder Default(Location location) => new(location);

    public LocationForecastBuilder WithWeather(DomainForecast<WeatherPoint> weather)
    {
        _forecast = _forecast with { Weather = weather };
        return this;
    }

    public LocationForecastBuilder WithAirQuality(DomainForecast<AirQualityPoint> airQuality)
    {
        _forecast = _forecast with { AirQuality = airQuality };
        return this;
    }

    public LocationForecastBuilder WithFlood(DomainForecast<FloodPoint> flood)
    {
        _forecast = _forecast with { Flood = flood };
        return this;
    }

    public LocationForecast Build() => _forecast;
}
