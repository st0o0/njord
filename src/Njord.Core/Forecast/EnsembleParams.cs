namespace Njord.Core.Forecast;

[Flags]
public enum EnsembleParams : long
{
    None = 0,
    TemperatureSpread = 1L << 0,
    PrecipitationSpread = 1L << 1,
    WindSpeedSpread = 1L << 2,
    PressureMslSpread = 1L << 3,
    CloudCoverSpread = 1L << 4,
    WindGustsSpread = 1L << 5,
    CapeSpread = 1L << 6,
    SnowfallSpread = 1L << 7,
    RainSpread = 1L << 8,
    DewPointSpread = 1L << 9,
    HumiditySpread = 1L << 10,
    SurfacePressureSpread = 1L << 11,
    WindDirectionSpread = 1L << 12,
    WetBulbTemperatureSpread = 1L << 13,
    WindSpeed100MSpread = 1L << 14,
}
