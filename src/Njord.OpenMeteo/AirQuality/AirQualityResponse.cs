using System.Text.Json.Serialization;

namespace Njord.OpenMeteo.AirQuality;

public sealed record AirQualityHourlyData(
    [property: JsonPropertyName("time")] long[] Time,
    [property: JsonPropertyName("pm2_5")] double?[]? Pm25 = null,
    [property: JsonPropertyName("pm10")] double?[]? Pm10 = null,
    [property: JsonPropertyName("ozone")] double?[]? Ozone = null,
    [property: JsonPropertyName("nitrogen_dioxide")] double?[]? NitrogenDioxide = null,
    [property: JsonPropertyName("european_aqi")] double?[]? EuropeanAqi = null,
    [property: JsonPropertyName("birch_pollen")] double?[]? BirchPollen = null,
    [property: JsonPropertyName("grass_pollen")] double?[]? GrassPollen = null,
    [property: JsonPropertyName("alder_pollen")] double?[]? AlderPollen = null,
    [property: JsonPropertyName("mugwort_pollen")] double?[]? MugwortPollen = null,
    [property: JsonPropertyName("ragweed_pollen")] double?[]? RagweedPollen = null,
    [property: JsonPropertyName("carbon_monoxide")] double?[]? CarbonMonoxide = null,
    [property: JsonPropertyName("sulphur_dioxide")] double?[]? SulphurDioxide = null,
    [property: JsonPropertyName("dust")] double?[]? Dust = null,
    [property: JsonPropertyName("ammonia")] double?[]? Ammonia = null,
    [property: JsonPropertyName("olive_pollen")] double?[]? OlivePollen = null,
    [property: JsonPropertyName("us_aqi")] double?[]? UsAqi = null,
    [property: JsonPropertyName("uv_index")] double?[]? UvIndex = null,
    [property: JsonPropertyName("uv_index_clear_sky")] double?[]? UvIndexClearSky = null,
    [property: JsonPropertyName("european_aqi_pm2_5")] double?[]? EuropeanAqiPm25 = null,
    [property: JsonPropertyName("european_aqi_pm10")] double?[]? EuropeanAqiPm10 = null,
    [property: JsonPropertyName("european_aqi_nitrogen_dioxide")] double?[]? EuropeanAqiNitrogenDioxide = null,
    [property: JsonPropertyName("european_aqi_ozone")] double?[]? EuropeanAqiOzone = null,
    [property: JsonPropertyName("european_aqi_sulphur_dioxide")] double?[]? EuropeanAqiSulphurDioxide = null,
    [property: JsonPropertyName("aerosol_optical_depth")] double?[]? AerosolOpticalDepth = null);

public sealed record AirQualityResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("hourly")] AirQualityHourlyData? Hourly);
