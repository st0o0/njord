using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Njord.Alerts.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Core.Configuration.OpenMeteo;
using Njord.Core.Configuration.Persistence;
using Njord.Core.Configuration.Polling;
using Njord.Pipeline.Configuration;
using Njord.Tests.Shared;

namespace Njord.Tests.Configuration;

// The commented variables of docker-compose.example.yml show the default of each option. They are compared
// with the options as the host binds them from the shipped appsettings.json (list defaults come from the
// code). A variable whose option has no default (locations, models, per-location overrides, an unset
// BudgetOverride) is an example value and is not compared; a block introduced by a comment containing
// "Alternative" lists values that deliberately differ from the defaults.
public sealed partial class ComposeExampleSpec
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    [GeneratedRegex(@"^\s*(?<commented>#?)- (?<name>Njord__[^=\s]+)=(?<value>.*?)\s*$")]
    private static partial Regex VariableLine();

    private sealed record Variable(string Name, string Key, string Value, bool Commented, bool Alternative);

    private static IReadOnlyList<Variable> Variables()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "docker-compose.example.yml");
        var variables = new List<Variable>();
        var alternative = false;
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                alternative = false;
            }
            else if (line.TrimStart().StartsWith('#') && line.Contains("Alternative", StringComparison.Ordinal))
            {
                alternative = true;
            }

            if (VariableLine().Match(line) is { Success: true } match)
            {
                var name = match.Groups["name"].Value;
                variables.Add(new Variable(
                    name,
                    name.Replace("__", ":", StringComparison.Ordinal),
                    match.Groups["value"].Value,
                    match.Groups["commented"].Length > 0,
                    alternative));
            }
        }

        return variables;
    }

    private static Dictionary<string, string?> Defaults()
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        Add<NjordOptions>(result);
        Add<PollingOptions>(result);
        Add<OpenMeteoOptions>(result);
        Add<StorageOptions>(result);
        Add<PersistenceOptions>(result);
        Add<MqttOptions>(result);
        Add<AlertOptions>(result);
        return result;
    }

    private static void Add<T>(Dictionary<string, string?> result) where T : class =>
        Flatten(JsonSerializer.SerializeToNode(AppSettingsDefaults.Options<T>(), Json), ConfigSections.PathOf<T>(), result);

    private static void Flatten(JsonNode? node, string path, Dictionary<string, string?> result)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    Flatten(value, $"{path}:{key}", result);
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    Flatten(array[i], $"{path}:{i}", result);
                }

                break;
            case JsonValue value:
                result[path] = value.ToString();
                break;
        }
    }

    private static bool SameValue(string actual, string expected)
    {
        if (TimeSpan.TryParse(actual, CultureInfo.InvariantCulture, out var a) && TimeSpan.TryParse(expected, CultureInfo.InvariantCulture, out var b)
            && actual.Contains(':') && expected.Contains(':'))
        {
            return a == b;
        }

        if (double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            return x == y;
        }

        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_example_declares_variables()
    {
        var variables = Variables();

        Assert.NotEmpty(variables);
    }

    [Fact]
    public void Every_commented_default_equals_the_value_bound_from_the_shipped_file()
    {
        var mismatches = Mismatches(Variables(), Defaults());

        Assert.True(mismatches.Count == 0, "Stale defaults in docker-compose.example.yml: " + string.Join("; ", mismatches));
    }

    [Fact]
    public void A_stale_default_is_reported_with_the_variable_name()
    {
        var defaults = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Njord:Polling:PollInterval"] = "01:00:00",
            ["Njord:ForecastDays"] = "4",
        };
        Variable[] variables =
        [
            new("Njord__Polling__PollInterval", "Njord:Polling:PollInterval", "00:30:00", true, false),
            new("Njord__ForecastDays", "Njord:ForecastDays", "4.0", true, false),
            new("Njord__Persistence__Provider", "Njord:Persistence:Provider", "PostgreSql", true, true),
        ];

        var mismatches = Mismatches(variables, defaults);

        Assert.Equal(["Njord__Polling__PollInterval: example says '00:30:00', default is '01:00:00'"], mismatches);
    }

    private static List<string> Mismatches(IEnumerable<Variable> variables, IReadOnlyDictionary<string, string?> defaults) =>
        [.. variables
            .Where(v => v is { Commented: true, Alternative: false } && v.Value.Length > 0)
            .Where(v => defaults.TryGetValue(v.Key, out var actual) && actual is not null && !SameValue(actual, v.Value))
            .Select(v => $"{v.Name}: example says '{v.Value}', default is '{defaults[v.Key]}'")];

    [Fact]
    public void Every_scalar_option_with_a_default_is_listed_in_the_example()
    {
        var listed = Variables().Select(v => v.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var optional = new[] { "Njord:Parameters" };

        var missing = Defaults()
            .Where(d => d.Value is not null && !char.IsDigit(d.Key[^1]))
            .Select(d => d.Key)
            .Where(key => !listed.Contains(key) && !listed.Any(l => l.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase)))
            .Where(key => !optional.Any(o => key.StartsWith(o, StringComparison.OrdinalIgnoreCase)))
            .Select(key => key.Replace(":", "__", StringComparison.Ordinal))
            .ToList();

        Assert.True(missing.Count == 0, "Options missing from docker-compose.example.yml: " + string.Join(", ", missing));
    }
}
