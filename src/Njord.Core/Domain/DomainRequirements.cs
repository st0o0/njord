using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;

namespace Njord.Core.Domain;

public readonly record struct DomainRequirements(
    WeatherParam Weather = default,
    AirQualityParam AirQuality = default,
    FloodParam Flood = default,
    MarineParam Marine = default)
{
    public static DomainRequirements operator |(DomainRequirements a, DomainRequirements b) => new(
        a.Weather | b.Weather,
        a.AirQuality | b.AirQuality,
        a.Flood | b.Flood,
        a.Marine | b.Marine);

    public bool Requires(DomainKind domain) => domain switch
    {
        DomainKind.Weather => Weather != default,
        DomainKind.AirQuality => AirQuality != default,
        DomainKind.Flood => Flood != default,
        DomainKind.Marine => Marine != default,
        _ => false,
    };
}
