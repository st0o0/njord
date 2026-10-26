using System.Text.Json.Serialization;

namespace Njord.OpenMeteo.Marine;

public enum MarineApiVariable
{
    [JsonStringEnumMemberName("wave_height")]
    WaveHeight,
    [JsonStringEnumMemberName("wave_period")]
    WavePeriod,
    [JsonStringEnumMemberName("wave_direction")]
    WaveDirection,
    [JsonStringEnumMemberName("sea_surface_temperature")]
    WaterTemperature,
    [JsonStringEnumMemberName("wind_wave_height")]
    WindWaveHeight,
    [JsonStringEnumMemberName("wind_wave_period")]
    WindWavePeriod,
    [JsonStringEnumMemberName("wind_wave_direction")]
    WindWaveDirection,
    [JsonStringEnumMemberName("wind_wave_peak_period")]
    WindWavePeakPeriod,
    [JsonStringEnumMemberName("swell_wave_height")]
    SwellWaveHeight,
    [JsonStringEnumMemberName("swell_wave_period")]
    SwellWavePeriod,
    [JsonStringEnumMemberName("swell_wave_direction")]
    SwellWaveDirection,
    [JsonStringEnumMemberName("swell_wave_peak_period")]
    SwellWavePeakPeriod,
    [JsonStringEnumMemberName("sea_level_height_msl")]
    SeaLevelHeight,
}
