using System.Text.Json.Serialization;
using Njord.Mqtt.Serialization;

namespace Njord.Mqtt.Payloads;

[JsonConverter(typeof(ConsensusHorizonPayloadConverter))]
public sealed record ConsensusHorizonPayload(IReadOnlyList<ConsensusParameterPayload> Parameters, ConsensusSpreadPayload? Spread);
