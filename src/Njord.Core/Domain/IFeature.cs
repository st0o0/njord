namespace Njord.Core.Domain;

// A toggleable feature: Name is unique and drives the config key (Category:Name) and the wire key (lowercase Name).
public interface IFeature
{
    string Name { get; }

    FeatureDescriptor Feature { get; }
}

public static class FeatureKeys
{
    public static string SectionKey(this IFeature feature) => $"{feature.Feature.Category}:{feature.Name}";

    public static string WireKey(this IFeature feature) => feature.Name.ToLowerInvariant();
}
