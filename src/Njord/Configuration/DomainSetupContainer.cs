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
            new ResolvedRequirements(FeatureResolution.RequiredParams(sp.GetServices<IFeatureRule>())));
    }
}
