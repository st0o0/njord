using Njord.Core.Domain;

namespace Njord.Core.Configuration;

// Explicit toggles per feature, bound hierarchically from Njord:Features:{Category}:{Name}. A feature without an
// entry follows the DefaultEnabled of its descriptor, so a new feature needs only its handler.
public sealed class FeatureOptions : Dictionary<string, Dictionary<string, bool>>
{
    public const string SectionName = "Njord:Features";

    public FeatureOptions()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    public IEnumerable<string> ConfiguredKeys() =>
        this.SelectMany(category => category.Value.Keys.Select(name => $"{category.Key}:{name}"));

    public void Set(string category, string name, bool enabled)
    {
        if (!TryGetValue(category, out var section))
        {
            section = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            this[category] = section;
        }

        section[name] = enabled;
    }

    public bool IsEnabled(IFeature feature)
    {
        if (TryGetValue(feature.Feature.Category.ToString(), out var section))
        {
            foreach (var (name, enabled) in section)
            {
                if (string.Equals(name, feature.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return enabled;
                }
            }
        }

        return feature.Feature.DefaultEnabled;
    }
}
