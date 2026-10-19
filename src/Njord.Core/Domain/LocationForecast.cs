using Njord.Model;

namespace Njord.Core.Domain;

public sealed record LocationForecast(
    Location Location,
    DomainForecast<WeatherPoint>? Weather = null,
    DomainForecast<AirQualityPoint>? AirQuality = null,
    DomainForecast<FloodPoint>? Flood = null,
    DomainForecast<SolarPoint>? Solar = null,
    DomainForecast<MarinePoint>? Marine = null)
{
    public LocationForecast With(DomainKind domain, IDomainForecast forecast) => domain switch
    {
        DomainKind.Weather => this with { Weather = Cast<WeatherPoint>(domain, forecast) },
        DomainKind.AirQuality => this with { AirQuality = Cast<AirQualityPoint>(domain, forecast) },
        DomainKind.Flood => this with { Flood = Cast<FloodPoint>(domain, forecast) },
        DomainKind.Solar => this with { Solar = Cast<SolarPoint>(domain, forecast) },
        DomainKind.Marine => this with { Marine = Cast<MarinePoint>(domain, forecast) },
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null)
    };

    private static DomainForecast<T> Cast<T>(DomainKind domain, IDomainForecast forecast)
        where T : IHasValidTime =>
        forecast as DomainForecast<T>
        ?? throw new ArgumentException(
            $"Forecast of type {forecast.GetType().Name} does not match domain {domain}.", nameof(forecast));
}
