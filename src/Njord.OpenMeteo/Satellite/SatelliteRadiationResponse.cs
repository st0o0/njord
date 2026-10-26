using System.Text.Json.Serialization;

namespace Njord.OpenMeteo.Satellite;

// Hourly satellite-observed irradiance in W/m2; global_tilted_irradiance follows the tilt and azimuth of the request.
public sealed record SatelliteRadiationHourlyData(
    [property: JsonPropertyName("time")] long[] Time,
    [property: JsonPropertyName("shortwave_radiation")] double?[]? ShortwaveRadiation = null,
    [property: JsonPropertyName("direct_radiation")] double?[]? DirectRadiation = null,
    [property: JsonPropertyName("diffuse_radiation")] double?[]? DiffuseRadiation = null,
    [property: JsonPropertyName("direct_normal_irradiance")] double?[]? DirectNormalIrradiance = null,
    [property: JsonPropertyName("global_tilted_irradiance")] double?[]? GlobalTiltedIrradiance = null);

public sealed record SatelliteRadiationResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("hourly")] SatelliteRadiationHourlyData? Hourly);
