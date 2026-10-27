using System.Collections.Immutable;
using Njord.Core.Configuration;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;

namespace Njord.Core.Domain;

public static class FeatureResolution
{
    public const string ComparisonFeatureName = "ModelComparison";

    public static IReadOnlyList<IFeatureRule> EnabledFeatures(IEnumerable<IFeatureRule> features) =>
        [.. features.Where(f => f.Enabled)];

    public static DomainRequirements RequiredParams(IEnumerable<IFeatureRule> features) =>
        EnabledFeatures(features).Aggregate(default(DomainRequirements), (all, f) => all | f.Descriptor.Requirements);

    public static DomainRequirements WithBaseParams(DomainRequirements requirements) => requirements with
    {
        Weather = requirements.AllWeather != default ? requirements.Weather | WeatherParams.WeatherCode : requirements.Weather,
        AirQuality = requirements.AllAirQuality != default ? requirements.AirQuality | AirQualityParams.Pm10 : requirements.AirQuality,
    };

    public static IReadOnlySet<DomainKind> RequiredDomains(IEnumerable<IFeatureRule> features)
    {
        var requirements = RequiredParams(features);
        return Enum.GetValues<DomainKind>().Where(requirements.Requires).ToHashSet();
    }

    public static void Configure(
        FeatureRequirementsOptions options,
        IEnumerable<IFeatureRule> features,
        WeatherOptions weather)
    {
        var enabled = EnabledFeatures(features);
        var raw = enabled.Aggregate(default(DomainRequirements),
            static (all, f) => all | f.Descriptor.Requirements);

        options.Requirements = WithBaseParams(raw);
        options.Domains = [.. Enum.GetValues<DomainKind>()
            .Where(options.Requirements.Requires).Order()];

        var comparison = enabled.Any(
            static rule => rule.Descriptor.Name == ComparisonFeatureName);
        options.WeatherModels = [.. comparison
            ? weather.EffectiveModels : [weather.PrimaryModel]];
        options.EnsembleModels = options.Requirements.Requires(DomainKind.Ensemble)
            ? [.. comparison ? weather.EffectiveModels : [weather.PrimaryModel]]
            : [];
    }
}
