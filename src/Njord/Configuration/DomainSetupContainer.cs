using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Features;
using Njord.Forecast;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class DomainSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddFeatures(configuration);

        services.AddSingleton<IReadOnlySet<DomainKind>>(static sp =>
            FeatureResolution.RequiredDomains(sp.GetServices<IFeatureRule>()));

        services.AddSingleton(static sp =>
        {
            var weather = sp.GetRequiredService<IOptions<WeatherOptions>>().Value;
            var comparison = sp.GetServices<IFeatureRule>().Any(static rule =>
                rule.Enabled && rule.Descriptor.Name == EffectiveWeatherModels.ComparisonFeatureName);
            var models = EffectiveWeatherModels.Resolve(weather, comparison);
            var ignored = models.Ignored(weather);
            if (ignored.Count > 0)
            {
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<DomainSetupContainer>().LogWarning(
                    "Weather models {IgnoredModels} are configured but ignored because Njord:Features:Weather:ModelComparison is disabled; only {PrimaryModel} is fetched",
                    string.Join(", ", ignored), models.Primary);
            }

            return models;
        });

        services.AddSingleton(static sp =>
            new ResolvedRequirements(FeatureResolution.RequiredParams(sp.GetServices<IFeatureRule>())));
    }
}
