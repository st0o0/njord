using System.Text.Json;
using Njord.Mqtt.Payloads;

namespace Njord.Mqtt.Serialization;

internal sealed class ConsensusHorizonPayloadConverter : WriteOnlyJsonConverter<ConsensusHorizonPayload>
{
    public override void Write(Utf8JsonWriter writer, ConsensusHorizonPayload value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var parameter in value.Parameters)
        {
            writer.WriteNullableNumber(parameter.Key, parameter.Median);
            writer.WriteNumber(parameter.ModelsKey, parameter.Models);
        }

        if (value.Spread is { } spread)
        {
            writer.WriteNullableNumber("_spread", spread.Spread);
            writer.WriteNullableNumber("_agreement", spread.Agreement);
        }

        writer.WriteEndObject();
    }
}
