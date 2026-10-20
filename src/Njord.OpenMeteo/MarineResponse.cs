using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

public sealed record MarineHourlyData(
    [property: JsonPropertyName("time")] long[] Time,
    [property: JsonPropertyName("wave_height")] double?[] WaveHeight,
    [property: JsonPropertyName("wave_period")] double?[] WavePeriod,
    [property: JsonPropertyName("wave_direction")] double?[] WaveDirection,
    [property: JsonPropertyName("ocean_current_velocity")] double?[]? OceanCurrentVelocity = null,
    [property: JsonPropertyName("ocean_current_direction")] double?[]? OceanCurrentDirection = null,
    [property: JsonPropertyName("sea_surface_temperature")] double?[]? SeaSurfaceTemperature = null);

public sealed record MarineResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("hourly")] MarineHourlyData? Hourly);
