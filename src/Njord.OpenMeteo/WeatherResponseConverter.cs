using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

// The ensemble API suffixes every hourly key with the model id when several models are requested
// (temperature_2m_<modelid>); a single model keeps the plain names. Member series (..._memberNN_...) are ignored.
internal sealed class WeatherResponseConverter : JsonConverter<WeatherResponse>
{
    private const string TimeKey = "time";
    private const string MemberPrefix = "member";

    private static readonly string[] BaseNames =
    [
        .. typeof(WeatherHourlyData).GetConstructors()[0].GetParameters()
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? typeof(WeatherHourlyData).GetProperty(p.Name!)?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .OfType<string>()
            .Where(n => n != TimeKey)
            .OrderByDescending(n => n.Length),
    ];

    public override WeatherResponse? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var latitude = root.TryGetProperty("latitude", out var lat) ? lat.GetDouble() : 0;
        var longitude = root.TryGetProperty("longitude", out var lon) ? lon.GetDouble() : 0;
        if (!root.TryGetProperty("hourly", out var hourly) || hourly.ValueKind != JsonValueKind.Object)
        {
            return new WeatherResponse(latitude, longitude, null);
        }

        var plain = new JsonObject();
        var perModel = new Dictionary<string, JsonObject>();
        var modelOrder = new List<string>();
        foreach (var property in hourly.EnumerateObject())
        {
            if (property.Name == TimeKey)
            {
                continue;
            }

            if (!TrySplit(property.Name, out var baseName, out var model))
            {
                continue;
            }

            if (model is null)
            {
                plain[baseName] = JsonNode.Parse(property.Value.GetRawText());
                continue;
            }

            if (!perModel.TryGetValue(model, out var group))
            {
                perModel[model] = group = [];
                modelOrder.Add(model);
            }

            group[baseName] = JsonNode.Parse(property.Value.GetRawText());
        }

        var time = hourly.TryGetProperty(TimeKey, out var t) ? JsonNode.Parse(t.GetRawText()) : new JsonArray();
        var context = OpenMeteoJsonContext.Default.WeatherHourlyData;
        WeatherHourlyData Build(JsonObject series)
        {
            series[TimeKey] = time?.DeepClone();
            return series.Deserialize(context)!;
        }

        var models = modelOrder.ToImmutableDictionary(m => m, m => Build(perModel[m]));
        return new WeatherResponse(latitude, longitude, plain.Count > 0 || models.IsEmpty ? Build(plain) : null, models);
    }

    public override void Write(Utf8JsonWriter writer, WeatherResponse value, JsonSerializerOptions options) =>
        throw new NotSupportedException();

    private static bool TrySplit(string key, out string baseName, out string? model)
    {
        foreach (var name in BaseNames)
        {
            if (key == name)
            {
                (baseName, model) = (name, null);
                return true;
            }

            if (key.Length > name.Length + 1 && key.StartsWith(name, StringComparison.Ordinal) && key[name.Length] == '_')
            {
                var suffix = key[(name.Length + 1)..];
                if (suffix.StartsWith(MemberPrefix, StringComparison.Ordinal))
                {
                    break;
                }

                (baseName, model) = (name, suffix);
                return true;
            }
        }

        (baseName, model) = (string.Empty, null);
        return false;
    }
}
