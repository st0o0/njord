namespace Njord.Forecast.Seasonal;

[Flags]
public enum SeasonalParams
{
    None = 0,
    TemperatureAnomaly = 1 << 0,
    PrecipitationAnomaly = 1 << 1,
    PrecipitationMean = 1 << 2,
}
