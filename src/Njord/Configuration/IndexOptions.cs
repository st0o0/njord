namespace Njord.Configuration;

public sealed class IndexOptions
{
    public bool Enabled { get; set; } = false;
    public IndexPreferences Preferences { get; set; } = new();
    public IDictionary<string, IndexPreferences> ScoreOverrides { get; set; } = new Dictionary<string, IndexPreferences>(StringComparer.OrdinalIgnoreCase);
    public IList<LocationIndexOverride> LocationOverrides { get; set; } = [];
}
