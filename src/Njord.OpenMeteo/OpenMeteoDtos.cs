using System.Text.Json;
using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

public sealed record OpenMeteoResponse(
    [property: JsonPropertyName("hourly")] OpenMeteoSeries? Hourly,
    [property: JsonPropertyName("daily")] OpenMeteoSeries? Daily);

[JsonConverter(typeof(OpenMeteoSeriesConverter))]
public sealed record OpenMeteoSeries(long[] Time, IReadOnlyDictionary<string, double?[]> Values)
{
    public double? ValueAt(string name, int index) =>
        Values.TryGetValue(name, out var values) && index < values.Length ? values[index] : null;
}

internal sealed record OpenMeteoErrorResponse(
    [property: JsonPropertyName("error")] bool Error,
    [property: JsonPropertyName("reason")] string? Reason);

internal sealed class OpenMeteoSeriesConverter : JsonConverter<OpenMeteoSeries>
{
    public override OpenMeteoSeries Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected a series object");
        }

        long[] time = [];
        var values = new Dictionary<string, double?[]>();

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString()!;
            reader.Read();

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                reader.Skip();
                continue;
            }

            if (name == "time")
            {
                time = JsonSerializer.Deserialize(ref reader, OpenMeteoJsonContext.Default.Int64Array) ?? [];
            }
            else
            {
                values[name] = JsonSerializer.Deserialize(ref reader, OpenMeteoJsonContext.Default.NullableDoubleArray) ?? [];
            }
        }

        return new OpenMeteoSeries(time, values);
    }

    public override void Write(Utf8JsonWriter writer, OpenMeteoSeries value, JsonSerializerOptions options) =>
        throw new NotSupportedException();
}
