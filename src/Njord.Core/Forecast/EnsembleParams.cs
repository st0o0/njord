namespace Njord.Core.Forecast;

[Flags]
public enum EnsembleParams : long
{
    None = 0,
    TemperatureSpread = 1L << 0,
    PrecipitationSpread = 1L << 1,
    WindSpeedSpread = 1L << 2,
}
