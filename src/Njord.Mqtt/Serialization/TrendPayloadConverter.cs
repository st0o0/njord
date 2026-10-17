using System.Text.Json;
using Njord.Compute.Contracts;
using Njord.Mqtt.Payloads;

namespace Njord.Mqtt.Serialization;

internal sealed class TrendPayloadConverter : WriteOnlyJsonConverter<TrendPayload>
{
    public override void Write(Utf8JsonWriter writer, TrendPayload value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var parameter in value.Parameters)
        {
            writer.WriteNullableString(parameter.Key.DirectionKey, parameter.Direction?.ToWireName());
            writer.WriteNullableNumber(parameter.Key.DeltaKey, parameter.Delta);
        }

        writer.WriteNullableString("weather_change", value.WeatherChange);
        writer.WriteNullableNumber("precip_starts", value.PrecipStarts);
        writer.WriteNullableNumber("precip_ends", value.PrecipEnds);
        writer.WriteNullableNumber("temp_max_in", value.TempMaxIn);
        writer.WriteNullableNumber("temp_min_in", value.TempMinIn);
        writer.WriteNullableString("stability", value.Stability?.ToWireName());
        writer.WriteNullableNumber("stability_ratio", value.StabilityRatio);
        writer.WriteNullableNumber("decay_rate", value.DecayRate);
        writer.WriteNullableNumber("reliable_hours", value.ReliableHours);

        writer.WriteEndObject();
    }
}
