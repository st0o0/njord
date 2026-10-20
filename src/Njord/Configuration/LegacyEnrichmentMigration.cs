using Njord.Core.Configuration;
using Njord.Core.Domain;

namespace Njord.Configuration;

// Maps the pre-0.5 Njord:Enrichments toggles onto Njord:Features. WeatherAlerts becomes every weather alert feature,
// every other legacy key names its feature directly. Explicit Njord:Features entries win over legacy ones.
public static class LegacyEnrichmentMigration
{
    public const string LegacySectionName = "Njord:Enrichments";
    public const string WeatherAlertsKey = "WeatherAlerts";

    public static IReadOnlyList<FeatureKind> WeatherAlertKinds { get; } =
    [
        FeatureKind.FrostAlert,
        FeatureKind.HeatAlert,
        FeatureKind.StormAlert,
        FeatureKind.RainAlert,
        FeatureKind.UvAlert,
        FeatureKind.FogAlert,
        FeatureKind.SnowAlert,
        FeatureKind.PressureDropAlert,
        FeatureKind.ThunderstormAlert,
        FeatureKind.IceAlert,
        FeatureKind.WindChillAlert,
        FeatureKind.VisibilityAlert,
        FeatureKind.TropicalNightAlert,
        FeatureKind.HumidityAlert,
    ];

    public static IReadOnlyDictionary<FeatureKind, bool> Expand(IConfiguration configuration, out IReadOnlyList<string> legacyKeys)
    {
        var expanded = new Dictionary<FeatureKind, bool>();
        var seen = new List<string>();

        foreach (var entry in configuration.GetSection(LegacySectionName).GetChildren())
        {
            if (!bool.TryParse(entry.Value, out var enabled))
            {
                continue;
            }

            if (string.Equals(entry.Key, WeatherAlertsKey, StringComparison.OrdinalIgnoreCase))
            {
                seen.Add(entry.Key);
                foreach (var kind in WeatherAlertKinds)
                {
                    expanded[kind] = enabled;
                }
            }
            else if (FeatureKindConfigMappings.TryParseConfigKey(entry.Key, out var kind))
            {
                seen.Add(entry.Key);
                expanded[kind] = enabled;
            }
        }

        legacyKeys = seen;
        return expanded;
    }

    // Registered before the Njord:Features binding, so explicit feature entries overwrite the migrated ones.
    public static IServiceCollection AddLegacyEnrichmentMigration(this IServiceCollection services, IConfiguration configuration)
    {
        var migrated = Expand(configuration, out var legacyKeys);
        if (legacyKeys.Count == 0)
        {
            return services;
        }

        services.AddOptions<FeatureOptions>().Configure(options =>
        {
            foreach (var (kind, enabled) in migrated)
            {
                options[kind] = enabled;
            }
        });
        services.AddHostedService(sp => new LegacyEnrichmentWarning(
            sp.GetRequiredService<ILogger<LegacyEnrichmentWarning>>(), legacyKeys));
        return services;
    }

    public sealed class LegacyEnrichmentWarning(ILogger<LegacyEnrichmentWarning> logger, IReadOnlyList<string> legacyKeys) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogWarning(
                "Legacy configuration section '{Section}' is deprecated and was migrated to '{Features}'. Legacy keys: {Keys}",
                LegacySectionName, FeatureOptions.SectionName, string.Join(", ", legacyKeys));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
