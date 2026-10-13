using Njord.Compute.Settings;

namespace Njord.Core.Configuration.Indices;

public static class IndexSettingsConversion
{
    public static IndexSettings ToSettings(this IndexOptions options, IEnumerable<string> locationNames) =>
        new(PreferenceResolver.Resolve(options, locationNames));
}
