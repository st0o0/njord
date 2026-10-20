using System.Reflection;

namespace Njord.Core.Configuration;

public sealed class EnrichmentOptions
{
    public const string SectionName = "Njord:Enrichments";

    private static readonly IReadOnlyDictionary<string, Func<EnrichmentOptions, bool>> Toggles = typeof(EnrichmentOptions)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.PropertyType == typeof(bool) && property.GetMethod is not null)
        .ToDictionary(
            property => property.Name,
            property => (Func<EnrichmentOptions, bool>)property.GetMethod!.CreateDelegate(typeof(Func<EnrichmentOptions, bool>)),
            StringComparer.Ordinal);

    public bool WeatherAlerts { get; set; } = true;
    public bool CommuteSafety { get; set; } = true;
    public bool PollenAlert { get; set; } = true;
    public bool Pm25Alert { get; set; } = true;

    public bool FloodRisk { get; set; }
    public bool FloodRain { get; set; }
    public bool SwimmingConditions { get; set; }
    public bool SurfConditions { get; set; }
    public bool VentilationWindow { get; set; }
    public bool ExerciseAdvisory { get; set; }
    public bool LaundryScore { get; set; }
    public bool PollenTrend { get; set; }
    public bool NightVentilation { get; set; }
    public bool ConfidenceTrend { get; set; }
    public bool SolarYield { get; set; }
    public bool GardeningAdvisory { get; set; }
    public bool OutdoorScore { get; set; }
    public bool BBQScore { get; set; }
    public bool FloodTimeline { get; set; }
    public bool ApproachingFront { get; set; }

    public static IReadOnlyCollection<string> Keys => Toggles.Keys.ToArray();

    public static bool IsKnownKey(string key) => Toggles.ContainsKey(key);

    public bool IsEnabled(string key) =>
        Toggles.TryGetValue(key, out var toggle)
            ? toggle(this)
            : throw new ArgumentException($"Unknown enrichment key '{key}'.", nameof(key));

    public IReadOnlySet<string> EnabledKeys() =>
        Toggles.Where(toggle => toggle.Value(this)).Select(toggle => toggle.Key).ToHashSet(StringComparer.Ordinal);
}
