namespace Njord.Core.Seasonal;

[Flags]
public enum SeasonalWeeklyParams : long
{
    None = 0,
    TemperatureEfi = 1L << 0,
    PrecipitationEfi = 1L << 1,
    TemperatureSot10 = 1L << 2,
    TemperatureSot90 = 1L << 3,
    PrecipitationSot90 = 1L << 4,
    TemperatureAnomalyGt0 = 1L << 5,
    TemperatureAnomalyGt1 = 1L << 6,
    TemperatureAnomalyGt2 = 1L << 7,
    TemperatureAnomalyLtM1 = 1L << 8,
    TemperatureAnomalyLtM2 = 1L << 9,
    PrecipitationAnomalyGt0 = 1L << 10,
    PrecipitationAnomalyGt10 = 1L << 11,
    PrecipitationAnomalyGt20 = 1L << 12,
}
