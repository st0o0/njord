namespace Njord.Core.Seasonal;

[Flags]
public enum SeasonalParams : long
{
    None = 0,
    TemperatureAnomaly = 1L << 0,
    PrecipitationAnomaly = 1L << 1,
    PrecipitationMean = 1L << 2,
    Temperature = 1L << 3,
    Snowfall = 1L << 4,
    SnowDepth = 1L << 5,
    CloudCover = 1L << 6,
    WindSpeed = 1L << 7,
    Pressure = 1L << 8,
    SunshineDuration = 1L << 9,
    ShortwaveRadiation = 1L << 10,
    WindGusts = 1L << 11,
    SeaSurfaceTemperature = 1L << 12,
}
