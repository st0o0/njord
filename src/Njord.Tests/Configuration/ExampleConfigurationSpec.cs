using System.Text.Json;

namespace Njord.Tests.Configuration;

public sealed class ExampleConfigurationSpec
{
    private static readonly JsonDocumentOptions Lenient = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [Fact]
    public void The_example_file_does_not_repeat_a_default_value_of_the_shipped_file()
    {
        var shipped = Leaves("appsettings.json");
        var example = Leaves("appsettings.Example.json");

        var duplicates = example
            .Where(e => shipped.TryGetValue(e.Key, out var value) && value == e.Value)
            .Select(e => $"{e.Key} = {e.Value}")
            .ToList();

        Assert.True(duplicates.Count == 0, "Duplicated defaults in appsettings.Example.json: " + string.Join(", ", duplicates));
    }

    [Fact]
    public void The_shipped_file_carries_the_scalar_defaults_under_the_Njord_section()
    {
        var shipped = Leaves("appsettings.json");

        Assert.Multiple(
            () => Assert.Equal("01:00:00", shipped["Njord:Polling:PollInterval"]),
            () => Assert.Equal("4", shipped["Njord:ForecastDays"]),
            () => Assert.Equal("1883", shipped["Njord:Mqtt:Port"]),
            () => Assert.Equal("Median", shipped["Njord:Enrichment:Consensus:Method"]));
    }

    private static Dictionary<string, string> Leaves(string file)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", file)), Lenient);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Flatten(document.RootElement, string.Empty, result);
        return result;
    }

    private static void Flatten(JsonElement element, string path, Dictionary<string, string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Flatten(property.Value, path.Length == 0 ? property.Name : $"{path}:{property.Name}", result);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Flatten(item, $"{path}:{index++}", result);
                }

                break;
            default:
                result[path] = element.GetRawText().Trim('"');
                break;
        }
    }
}
