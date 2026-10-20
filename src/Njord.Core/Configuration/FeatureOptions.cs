using Njord.Core.Domain;

namespace Njord.Core.Configuration;

// Explicit toggles per feature, bound from Njord:Features. A feature without an entry follows the
// DefaultEnabled of its descriptor, so a new feature needs one enum member and one descriptor.
public sealed class FeatureOptions : Dictionary<FeatureKind, bool>
{
    public const string SectionName = "Njord:Features";

    public bool IsEnabled(FeatureDescriptor feature) =>
        TryGetValue(feature.Kind, out var enabled) ? enabled : feature.DefaultEnabled;
}
