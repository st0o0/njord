namespace Njord.Forecast.Marine;

[Flags]
public enum MarineParams
{
    None = 0,
    WaveHeight = 1 << 0,
    WavePeriod = 1 << 1,
    WaveDirection = 1 << 2,
    WaterTemperature = 1 << 3,
    WindWaveHeight = 1 << 4,
    WindWavePeriod = 1 << 5,
    WindWaveDirection = 1 << 6,
    WindWavePeakPeriod = 1 << 7,
    SwellWaveHeight = 1 << 8,
    SwellWavePeriod = 1 << 9,
    SwellWaveDirection = 1 << 10,
    SwellWavePeakPeriod = 1 << 11,
    SeaLevelHeight = 1 << 12,
    OceanCurrentVelocity = 1 << 13,
    OceanCurrentDirection = 1 << 14,
    SecondarySwellWaveHeight = 1 << 15,
    SecondarySwellWaveDirection = 1 << 16,
    SecondarySwellWavePeriod = 1 << 17,
    TertiarySwellWaveHeight = 1 << 18,
    TertiarySwellWaveDirection = 1 << 19,
    TertiarySwellWavePeriod = 1 << 20,
    InvertBarometerHeight = 1 << 21,
}
