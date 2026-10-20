using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

[JsonSerializable(typeof(WeatherResponse))]
[JsonSerializable(typeof(AirQualityResponse))]
[JsonSerializable(typeof(FloodResponse))]
[JsonSerializable(typeof(MarineResponse))]
[JsonSerializable(typeof(GeocodingResponse))]
internal sealed partial class OpenMeteoJsonContext : JsonSerializerContext;
