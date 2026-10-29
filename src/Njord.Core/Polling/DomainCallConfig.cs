namespace Njord.Core.Polling;

public sealed record DomainCallConfig(
    int ForecastDays = 4,
    int ForecastModelCount = 0,
    int EnsembleDays = 4,
    int EnsembleModelCount = 1,
    int AirQualityDays = 5,
    int FloodDays = 92,
    bool FloodIncludeEnsemble = false,
    int MarineDays = 8,
    int SatellitePastHours = 6,
    int SeasonalMonths = 3);
