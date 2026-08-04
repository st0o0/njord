namespace Njord.Configuration;

public sealed class LocationIndexOverride
{
    public string Location { get; set; } = string.Empty;
    public IndexPreferences Preferences { get; set; } = new();
    public IDictionary<string, IndexPreferences> ScoreOverrides { get; set; } = new Dictionary<string, IndexPreferences>(StringComparer.OrdinalIgnoreCase);
}
