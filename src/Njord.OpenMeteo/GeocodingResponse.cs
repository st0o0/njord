using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

public sealed record GeocodingResult(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("elevation")] double? Elevation = null,
    [property: JsonPropertyName("timezone")] string? Timezone = null,
    [property: JsonPropertyName("country")] string? Country = null);

public sealed record GeocodingResponse(
    [property: JsonPropertyName("results")] GeocodingResult[]? Results);
