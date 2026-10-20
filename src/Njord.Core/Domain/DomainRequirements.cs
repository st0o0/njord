using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Seasonal;
using Njord.Forecast.Weather;

namespace Njord.Core.Domain;

public readonly record struct DomainRequirements(
    WeatherParams Weather = default,
    AirQualityParams AirQuality = default,
    FloodParams Flood = default,
    MarineParams Marine = default,
    WeatherParams OptionalWeather = default,
    AirQualityParams OptionalAirQuality = default,
    FloodParams OptionalFlood = default,
    MarineParams OptionalMarine = default,
    SeasonalParams Seasonal = default,
    SeasonalParams OptionalSeasonal = default)
{
    public WeatherParams AllWeather => Weather | OptionalWeather;

    public AirQualityParams AllAirQuality => AirQuality | OptionalAirQuality;

    public FloodParams AllFlood => Flood | OptionalFlood;

    public MarineParams AllMarine => Marine | OptionalMarine;

    public SeasonalParams AllSeasonal => Seasonal | OptionalSeasonal;

    public static DomainRequirements operator |(DomainRequirements a, DomainRequirements b) => new(
        a.Weather | b.Weather,
        a.AirQuality | b.AirQuality,
        a.Flood | b.Flood,
        a.Marine | b.Marine,
        a.OptionalWeather | b.OptionalWeather,
        a.OptionalAirQuality | b.OptionalAirQuality,
        a.OptionalFlood | b.OptionalFlood,
        a.OptionalMarine | b.OptionalMarine,
        a.Seasonal | b.Seasonal,
        a.OptionalSeasonal | b.OptionalSeasonal);

    public bool Requires(DomainKind domain) => domain switch
    {
        DomainKind.Weather => AllWeather != default,
        DomainKind.AirQuality => AllAirQuality != default,
        DomainKind.Flood => AllFlood != default,
        DomainKind.Marine => AllMarine != default,
        DomainKind.Seasonal => AllSeasonal != default,
        _ => false,
    };
}
