using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

public sealed record AirQualityHourlyData(
    [property: JsonPropertyName("time")] long[] Time,
    [property: JsonPropertyName("pm2_5")] double?[] Pm25,
    [property: JsonPropertyName("pm10")] double?[] Pm10,
    [property: JsonPropertyName("ozone")] double?[] Ozone,
    [property: JsonPropertyName("nitrogen_dioxide")] double?[]? NitrogenDioxide = null,
    [property: JsonPropertyName("european_aqi")] double?[]? EuropeanAqi = null,
    [property: JsonPropertyName("birch_pollen")] double?[]? BirchPollen = null,
    [property: JsonPropertyName("grass_pollen")] double?[]? GrassPollen = null,
    [property: JsonPropertyName("alder_pollen")] double?[]? AlderPollen = null,
    [property: JsonPropertyName("mugwort_pollen")] double?[]? MugwortPollen = null,
    [property: JsonPropertyName("ragweed_pollen")] double?[]? RagweedPollen = null);

public sealed record AirQualityResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("hourly")] AirQualityHourlyData? Hourly);
