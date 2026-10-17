using System.Text.Json.Serialization;
using Njord.Mqtt.Payloads;

namespace Njord.Mqtt.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false)]
[JsonSerializable(typeof(AlertPayload))]
[JsonSerializable(typeof(DerivedHorizonPayload))]
[JsonSerializable(typeof(DerivedMetaPayload))]
[JsonSerializable(typeof(IndexDayPayload))]
[JsonSerializable(typeof(TrendPayload))]
[JsonSerializable(typeof(ConsensusHorizonPayload))]
[JsonSerializable(typeof(ForecastHorizonPayload))]
internal sealed partial class MqttPayloadJsonContext : JsonSerializerContext;
