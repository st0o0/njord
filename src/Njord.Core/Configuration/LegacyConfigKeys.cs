using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace Njord.Core.Configuration;

// The root-level keys of versions before 0.5.0 and their per-concern locations. Startup validation rejects
// them in every configuration source; the override file, which njord itself wrote them into, is migrated.
// Delete this class together with its callers one release after 0.5.0.
public static class LegacyConfigKeys
{
    private const string Root = "Njord";

    public sealed record Move(string OldKey, string NewPath)
    {
        public string OldFullKey => $"{Root}:{OldKey}";

        public string NewFullKey => $"{Root}:{NewPath}";
    }

    public static IReadOnlyList<Move> All { get; } =
    [
        new("PollInterval", "Polling:PollInterval"),
        new("DiscoveryInterval", "Polling:DiscoveryInterval"),
        new("BudgetOverride", "Polling:BudgetOverride"),
        new("OpenMeteoBaseUrl", "OpenMeteo:BaseUrl"),
        new("PersistencePath", "Storage:PersistencePath"),
    ];

    public static IReadOnlyList<string> Failures(IConfiguration configuration) =>
        [.. All
            .Where(move => configuration.GetSection(move.OldFullKey).Exists())
            .Select(move => $"Configuration key '{move.OldFullKey}' was removed in 0.5.0; use '{move.NewFullKey}' "
                + $"(environment variable '{move.NewFullKey.Replace(":", "__")}') instead.")];

    // Moves the old keys of a parsed override file below their new sections. A new key that is already
    // present wins over its old spelling. Returns the old keys that were found.
    public static IReadOnlyList<string> Migrate(JsonObject root)
    {
        var migrated = new List<string>();
        if (FindProperty(root, Root) is not { Value: JsonObject njord })
        {
            return migrated;
        }

        foreach (var move in All)
        {
            if (FindProperty(njord, move.OldKey) is not { } old)
            {
                continue;
            }

            njord.Remove(old.Key);
            var segments = move.NewPath.Split(':');
            var target = njord;
            foreach (var segment in segments[..^1])
            {
                if (FindProperty(target, segment) is { Value: JsonObject existing })
                {
                    target = existing;
                    continue;
                }

                var created = new JsonObject();
                target[segment] = created;
                target = created;
            }

            if (FindProperty(target, segments[^1]) is null)
            {
                target[segments[^1]] = old.Value;
            }

            migrated.Add(move.OldFullKey);
        }

        return migrated;
    }

    private static KeyValuePair<string, JsonNode?>? FindProperty(JsonObject owner, string name)
    {
        foreach (var property in owner)
        {
            if (string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return property;
            }
        }

        return null;
    }
}
