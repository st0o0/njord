using System.Text.Json.Serialization;

namespace Njord.OpenMeteo.Marine;

public sealed record MarineHourlyData(
    [property: JsonPropertyName("time")] long[] Time,
    [property: JsonPropertyName("wave_height")] double?[]? WaveHeight = null,
    [property: JsonPropertyName("wave_period")] double?[]? WavePeriod = null,
    [property: JsonPropertyName("wave_direction")] double?[]? WaveDirection = null,
    [property: JsonPropertyName("ocean_current_velocity")] double?[]? OceanCurrentVelocity = null,
    [property: JsonPropertyName("ocean_current_direction")] double?[]? OceanCurrentDirection = null,
    [property: JsonPropertyName("sea_surface_temperature")] double?[]? SeaSurfaceTemperature = null,
    [property: JsonPropertyName("wind_wave_height")] double?[]? WindWaveHeight = null,
    [property: JsonPropertyName("wind_wave_period")] double?[]? WindWavePeriod = null,
    [property: JsonPropertyName("wind_wave_direction")] double?[]? WindWaveDirection = null,
    [property: JsonPropertyName("wind_wave_peak_period")] double?[]? WindWavePeakPeriod = null,
    [property: JsonPropertyName("swell_wave_height")] double?[]? SwellWaveHeight = null,
    [property: JsonPropertyName("swell_wave_period")] double?[]? SwellWavePeriod = null,
    [property: JsonPropertyName("swell_wave_direction")] double?[]? SwellWaveDirection = null,
    [property: JsonPropertyName("swell_wave_peak_period")] double?[]? SwellWavePeakPeriod = null,
    [property: JsonPropertyName("sea_level_height_msl")] double?[]? SeaLevelHeight = null);

public sealed record MarineResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("hourly")] MarineHourlyData? Hourly);
