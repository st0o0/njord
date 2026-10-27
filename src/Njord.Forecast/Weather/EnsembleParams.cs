namespace Njord.Forecast.Weather;

[Flags]
public enum EnsembleParams : long
{
    None = 0,
    TemperatureSpread = 1L << 0,
    PrecipitationSpread = 1L << 1,
    WindSpeedSpread = 1L << 2,
    Models = 1L << 3,
}
