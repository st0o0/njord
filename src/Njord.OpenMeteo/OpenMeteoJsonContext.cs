using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

[JsonSerializable(typeof(WeatherResponse))]
[JsonSerializable(typeof(WeatherHourlyData))]
[JsonSerializable(typeof(AirQualityResponse))]
[JsonSerializable(typeof(FloodResponse))]
[JsonSerializable(typeof(MarineResponse))]
[JsonSerializable(typeof(SeasonalResponse))]
[JsonSerializable(typeof(SatelliteRadiationResponse))]
internal sealed partial class OpenMeteoJsonContext : JsonSerializerContext;
