namespace Njord.Core.Seasonal;

[Flags]
public enum SeasonalParams : long
{
    None = 0,
    TemperatureAnomaly = 1L << 0,
    PrecipitationAnomaly = 1L << 1,
    PrecipitationMean = 1L << 2,
}
