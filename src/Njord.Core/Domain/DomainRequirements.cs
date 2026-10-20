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
    MarineParam Marine = default,
    WeatherParam OptionalWeather = default,
    AirQualityParam OptionalAirQuality = default,
    FloodParam OptionalFlood = default,
    MarineParam OptionalMarine = default)
{
    public WeatherParam AllWeather => Weather | OptionalWeather;

    public AirQualityParam AllAirQuality => AirQuality | OptionalAirQuality;

    public FloodParam AllFlood => Flood | OptionalFlood;

    public MarineParam AllMarine => Marine | OptionalMarine;

    public static DomainRequirements operator |(DomainRequirements a, DomainRequirements b) => new(
        a.Weather | b.Weather,
        a.AirQuality | b.AirQuality,
        a.Flood | b.Flood,
        a.Marine | b.Marine,
        a.OptionalWeather | b.OptionalWeather,
        a.OptionalAirQuality | b.OptionalAirQuality,
        a.OptionalFlood | b.OptionalFlood,
        a.OptionalMarine | b.OptionalMarine);

    public bool Requires(DomainKind domain) => domain switch
    {
        DomainKind.Weather => AllWeather != default,
        DomainKind.AirQuality => AllAirQuality != default,
        DomainKind.Flood => AllFlood != default,
        DomainKind.Marine => AllMarine != default,
        _ => false,
    };
}
