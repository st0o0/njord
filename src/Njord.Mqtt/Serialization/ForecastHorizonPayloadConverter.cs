using System.Text.Json;
using Njord.Mqtt.Payloads;

namespace Njord.Mqtt.Serialization;

internal sealed class ForecastHorizonPayloadConverter : WriteOnlyJsonConverter<ForecastHorizonPayload>
{
    public override void Write(Utf8JsonWriter writer, ForecastHorizonPayload value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var entry in value.Entries)
        {
            if (entry.Number is { } number)
            {
                writer.WriteNumber(entry.Key, number);
            }
            else if (entry.Text is { } text)
            {
                writer.WriteString(entry.Key, text);
            }
        }

        writer.WriteEndObject();
    }
}
