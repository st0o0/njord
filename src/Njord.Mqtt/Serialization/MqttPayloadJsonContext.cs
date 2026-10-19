using System.Text.Json.Serialization;
using Njord.Mqtt.Payloads;

namespace Njord.Mqtt.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false)]
[JsonSerializable(typeof(AlertPayload))]
[JsonSerializable(typeof(WeatherPayload))]
[JsonSerializable(typeof(AirQualityPayload))]
[JsonSerializable(typeof(FloodPayload))]
[JsonSerializable(typeof(SolarPayload))]
[JsonSerializable(typeof(MarinePayload))]
internal sealed partial class MqttPayloadJsonContext : JsonSerializerContext;
