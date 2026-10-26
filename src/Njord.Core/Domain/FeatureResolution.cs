using System.Numerics;
using Microsoft.Extensions.Logging;
using Njord.Core.Configuration;
using Njord.Core.Polling;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;

namespace Njord.Core.Domain;

public static class FeatureResolution
{
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

    public static FetchPlan BuildFetchPlan(IEnumerable<IFeatureRule> features, WeatherOptions weather, ILogger logger)
    {
        var enabled = EnabledFeatures(features);
        var required = enabled.Aggregate(default(DomainRequirements), (all, f) => all | f.Descriptor.Requirements);
        var domains = Enum.GetValues<DomainKind>().Where(required.Requires).ToHashSet();
        var requirements = WithBaseParams(required);

        var comparison = enabled.Any(static rule => rule.Descriptor.Name == EffectiveWeatherModels.ComparisonFeatureName);
        var models = EffectiveWeatherModels.Resolve(weather, comparison);
        var ignored = models.Ignored(weather);
        if (ignored.Count > 0)
        {
            logger.LogWarning(
                "Weather models {IgnoredModels} are configured but ignored because Njord:Features:Weather:ModelComparison is disabled; only {PrimaryModel} is fetched",
                string.Join(", ", ignored), models.Primary);
        }

        var weatherCallWeight = requirements.Requires(DomainKind.Weather)
            ? CallWeight.Of(BitOperations.PopCount((ulong)requirements.AllWeather), weather.ForecastDays) * models.Count
            : 0;

        return new FetchPlan(domains, requirements, models, weatherCallWeight);
    }
}
