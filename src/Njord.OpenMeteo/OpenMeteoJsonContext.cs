using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

[JsonSerializable(typeof(WeatherResponse))]
[JsonSerializable(typeof(AirQualityResponse))]
[JsonSerializable(typeof(FloodResponse))]
[JsonSerializable(typeof(GeocodingResponse))]
[JsonSerializable(typeof(OpenMeteoErrorResponse))]
internal sealed partial class OpenMeteoJsonContext : JsonSerializerContext;
