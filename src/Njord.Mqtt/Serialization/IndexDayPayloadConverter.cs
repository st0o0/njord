using System.Text.Json;
using Njord.Compute.Contracts;
using Njord.Mqtt.Payloads;

namespace Njord.Mqtt.Serialization;

internal sealed class IndexDayPayloadConverter : WriteOnlyJsonConverter<IndexDayPayload>
{
    public override void Write(Utf8JsonWriter writer, IndexDayPayload value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var score in value.Scores)
        {
            writer.WriteNumber(score.Kind.ToWireName(), score.Score);
        }

        writer.WriteNumber("hours_included", value.HoursIncluded);

        if (value.Today is { } today)
        {
            writer.WriteNullableNumber("frost_hours", today.FrostHours);
            writer.WriteNullableNumber("frost_confidence", today.FrostConfidence);
            writer.WriteNullableString("vpd_category", today.VpdCategory?.ToWireName());
            writer.WriteNullableNumber("vpd_kpa", today.VpdKpa);
        }

        foreach (var score in value.Scores)
        {
            if (score.Envelope is not { } envelope)
            {
                continue;
            }

            var key = score.Kind.ToWireName();
            writer.WriteNumber($"{key}_min", envelope.Min);
            writer.WriteNumber($"{key}_max", envelope.Max);
            writer.WriteNumber($"{key}_confidence", envelope.Confidence);
        }

        writer.WriteEndObject();
    }
}
