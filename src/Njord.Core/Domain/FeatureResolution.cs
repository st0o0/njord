using Njord.Forecast;

namespace Njord.Core.Domain;

public static class FeatureResolution
{
    public static IReadOnlyList<IFeatureRule> EnabledFeatures(IEnumerable<IFeatureRule> features) =>
        [.. features.Where(f => f.Enabled)];

    public static DomainRequirements RequiredParams(IEnumerable<IFeatureRule> features) =>
        EnabledFeatures(features).Aggregate(default(DomainRequirements), (all, f) => all | f.Descriptor.Requirements);

    public static IReadOnlySet<DomainKind> RequiredDomains(IEnumerable<IFeatureRule> features)
    {
        var requirements = RequiredParams(features);
        return Enum.GetValues<DomainKind>().Where(requirements.Requires).ToHashSet();
    }
}
