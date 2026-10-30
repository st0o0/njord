namespace Njord.Core.Polling;

public sealed record DomainCallConfig
{
    public int ForecastDays { get; set; } = 4;
    public int ForecastModelCount { get; set; }
    public int EnsembleDays { get; set; } = 4;
    public int EnsembleModelCount { get; set; } = 1;
    public int AirQualityDays { get; set; } = 5;
    public int FloodDays { get; set; } = 92;
    public bool FloodIncludeEnsemble { get; set; }
    public int MarineDays { get; set; } = 8;
    public int SatellitePastHours { get; set; } = 6;
    public int SeasonalMonths { get; set; } = 3;
}
