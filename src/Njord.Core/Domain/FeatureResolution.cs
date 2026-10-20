using Njord.Core.Configuration;
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

    [Obsolete("Use the IFeatureRule overloads")]
    public static IReadOnlyList<IFeature> AllFeatures(IEnumerable<IDomainHandler> handlers) =>
        [.. handlers.SelectMany(FeaturesOf)];

    [Obsolete("Use the IFeatureRule overloads")]
    public static IReadOnlyList<IFeature> EnabledFeatures(FeatureOptions options, IEnumerable<IDomainHandler> handlers) =>
        [.. AllFeatures(handlers).Where(options.IsEnabled)];

    [Obsolete("Use the IFeatureRule overloads")]
    public static IReadOnlyList<IDomainHandler> EnabledHandlers(FeatureOptions options, IEnumerable<IDomainHandler> handlers) =>
        [.. handlers.Where(handler => FeaturesOf(handler).Any(options.IsEnabled))];

    [Obsolete("Use the IFeatureRule overloads")]
    public static DomainRequirements RequiredParams(FeatureOptions options, IEnumerable<IDomainHandler> handlers) =>
        EnabledFeatures(options, handlers).Aggregate(default(DomainRequirements), (all, feature) => all | feature.Feature.Requirements);

    [Obsolete("Use the IFeatureRule overloads")]
    public static IReadOnlySet<DomainKind> RequiredDomains(FeatureOptions options, IEnumerable<IDomainHandler> handlers)
    {
        var requirements = RequiredParams(options, handlers);
        return Enum.GetValues<DomainKind>().Where(requirements.Requires).ToHashSet();
    }

    private static IReadOnlyList<IFeature> FeaturesOf(IDomainHandler handler) =>
        handler is IMultiFeatureHandler multi ? multi.Features : [handler];
}
