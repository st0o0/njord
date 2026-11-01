using Njord.Core.AirQuality;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Core.Seasonal;

namespace Njord.Core.Domain;

public sealed record DomainRequirements(
    ForecastParams Forecast = default,
    ForecastParams OptionalForecast = default,
    EnsembleParams Ensemble = default,
    EnsembleParams OptionalEnsemble = default,
    AirQualityParams AirQuality = default,
    AirQualityParams OptionalAirQuality = default,
    FloodParams Flood = default,
    FloodParams OptionalFlood = default,
    MarineParams Marine = default,
    MarineParams OptionalMarine = default,
    SeasonalParams Seasonal = default,
    SeasonalParams OptionalSeasonal = default,
    SeasonalWeeklyParams SeasonalWeekly = default,
    SeasonalWeeklyParams OptionalSeasonalWeekly = default)
{
    public DomainRequirements WithBaseParams() => this with
    {
        Forecast = AllForecast != default ? Forecast | ForecastParams.WeatherCode : Forecast,
    };

    public ForecastParams AllForecast => Forecast | OptionalForecast;

    public AirQualityParams AllAirQuality => AirQuality | OptionalAirQuality;

    public FloodParams AllFlood => Flood | OptionalFlood;

    public MarineParams AllMarine => Marine | OptionalMarine;

    public SeasonalParams AllSeasonal => Seasonal | OptionalSeasonal;

    public SeasonalWeeklyParams AllSeasonalWeekly => SeasonalWeekly | OptionalSeasonalWeekly;


    public EnsembleParams AllEnsemble => Ensemble | OptionalEnsemble;

    public static DomainRequirements operator |(DomainRequirements a, DomainRequirements b) => new(
        a.Forecast | b.Forecast,
        a.OptionalForecast | b.OptionalForecast,
        a.Ensemble | b.Ensemble,
        a.OptionalEnsemble | b.OptionalEnsemble,
        a.AirQuality | b.AirQuality,
        a.OptionalAirQuality | b.OptionalAirQuality,
        a.Flood | b.Flood,
        a.OptionalFlood | b.OptionalFlood,
        a.Marine | b.Marine,
        a.OptionalMarine | b.OptionalMarine,
        a.Seasonal | b.Seasonal,
        a.OptionalSeasonal | b.OptionalSeasonal,
        a.SeasonalWeekly | b.SeasonalWeekly,
        a.OptionalSeasonalWeekly | b.OptionalSeasonalWeekly);

    public bool RequiresAnyDomain() =>
        AllForecast != default || AllAirQuality != default || AllFlood != default
        || AllMarine != default || AllSeasonal != default || AllSeasonalWeekly != default || AllEnsemble != default;

    public bool Requires(DomainKind domain) => domain switch
    {
        DomainKind.Forecast => AllForecast != default,
        DomainKind.AirQuality => AllAirQuality != default,
        DomainKind.Flood => AllFlood != default,
        DomainKind.Marine => AllMarine != default,
        DomainKind.Seasonal => AllSeasonal != default || AllSeasonalWeekly != default,
        DomainKind.Ensemble => AllEnsemble != default,
        _ => false,
    };
}
