namespace Njord.Forecast.Marine;

[Flags]
public enum MarineParam
{
    None = 0,
    WaveHeight = 1 << 0,
    WavePeriod = 1 << 1,
    WaveDirection = 1 << 2,
    WaterTemperature = 1 << 3,
}
