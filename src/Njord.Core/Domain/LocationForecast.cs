using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Solar;
using Njord.Forecast.Weather;

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

    public IDomainForecast? Get(DomainKind domain) => domain switch
    {
        DomainKind.Weather => Weather,
        DomainKind.AirQuality => AirQuality,
        DomainKind.Flood => Flood,
        DomainKind.Solar => Solar,
        DomainKind.Marine => Marine,
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, null)
    };

    public bool HasFields(DomainRequirements required) =>
        HasFields(Weather, required.Weather, static (point, param) => point.HasFields(param))
        && HasFields(AirQuality, required.AirQuality, static (point, param) => point.HasFields(param))
        && HasFields(Flood, required.Flood, static (point, param) => point.HasFields(param))
        && HasFields(Marine, required.Marine, static (point, param) => point.HasFields(param));

    private static bool HasFields<TPoint, TParam>(DomainForecast<TPoint>? forecast, TParam required, Func<TPoint, TParam, bool> check)
        where TPoint : IHasValidTime
        where TParam : struct, Enum =>
        EqualityComparer<TParam>.Default.Equals(required, default)
        || forecast is { Points.IsDefaultOrEmpty: false } && check(forecast.Points[0], required);

    private static DomainForecast<T> Cast<T>(DomainKind domain, IDomainForecast forecast)
        where T : IHasValidTime =>
        forecast as DomainForecast<T>
        ?? throw new ArgumentException(
            $"Forecast of type {forecast.GetType().Name} does not match domain {domain}.", nameof(forecast));
}
