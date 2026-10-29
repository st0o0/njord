using Njord.Core.AirQuality;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Core.Seasonal;
using Njord.Core.Solar;

namespace Njord.Core.Domain;

public sealed record DomainRequirements(
    ForecastParams Forecast = default,
    AirQualityParams AirQuality = default,
    FloodParams Flood = default,
    MarineParams Marine = default,
    ForecastParams OptionalForecast = default,
    AirQualityParams OptionalAirQuality = default,
    FloodParams OptionalFlood = default,
    MarineParams OptionalMarine = default,
    SeasonalParams Seasonal = default,
    SeasonalParams OptionalSeasonal = default,
    SatelliteParams Satellite = default,
    SatelliteParams OptionalSatellite = default,
    EnsembleParams Ensemble = default,
    EnsembleParams OptionalEnsemble = default)
{
    public DomainRequirements WithBaseParams() => this with
    {
        Forecast = AllForecast != default ? Forecast | ForecastParams.WeatherCode : Forecast,
        AirQuality = AllAirQuality != default ? AirQuality | AirQualityParams.Pm10 : AirQuality,
    };

    public ForecastParams AllForecast => Forecast | OptionalForecast;

    public AirQualityParams AllAirQuality => AirQuality | OptionalAirQuality;

    public FloodParams AllFlood => Flood | OptionalFlood;

    public MarineParams AllMarine => Marine | OptionalMarine;

    public SeasonalParams AllSeasonal => Seasonal | OptionalSeasonal;

    public SatelliteParams AllSatellite => Satellite | OptionalSatellite;

    public EnsembleParams AllEnsemble => Ensemble | OptionalEnsemble;

    public static DomainRequirements operator |(DomainRequirements a, DomainRequirements b) => new(
        a.Forecast | b.Forecast,
        a.AirQuality | b.AirQuality,
        a.Flood | b.Flood,
        a.Marine | b.Marine,
        a.OptionalForecast | b.OptionalForecast,
        a.OptionalAirQuality | b.OptionalAirQuality,
        a.OptionalFlood | b.OptionalFlood,
        a.OptionalMarine | b.OptionalMarine,
        a.Seasonal | b.Seasonal,
        a.OptionalSeasonal | b.OptionalSeasonal,
        a.Satellite | b.Satellite,
        a.OptionalSatellite | b.OptionalSatellite,
        a.Ensemble | b.Ensemble,
        a.OptionalEnsemble | b.OptionalEnsemble);

    public bool Requires(DomainKind domain) => domain switch
    {
        DomainKind.Forecast => AllForecast != default,
        DomainKind.AirQuality => AllAirQuality != default,
        DomainKind.Flood => AllFlood != default,
        DomainKind.Marine => AllMarine != default,
        DomainKind.Seasonal => AllSeasonal != default,
        DomainKind.Satellite => AllSatellite != default,
        DomainKind.Ensemble => AllEnsemble != default,
        _ => false,
    };
}
