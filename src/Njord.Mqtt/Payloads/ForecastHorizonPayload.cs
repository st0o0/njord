using System.Text.Json.Serialization;
using Njord.Mqtt.Serialization;

namespace Njord.Mqtt.Payloads;

[JsonConverter(typeof(ForecastHorizonPayloadConverter))]
public sealed record ForecastHorizonPayload(IReadOnlyList<ForecastHorizonEntryPayload> Entries);
