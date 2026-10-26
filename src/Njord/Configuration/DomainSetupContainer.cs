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
        services.AddFeatures();

        services.AddSingleton(static sp => FeatureResolution.BuildFetchPlan(
            sp.GetServices<IFeatureRule>(),
            sp.GetRequiredService<IOptions<WeatherOptions>>().Value,
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<DomainSetupContainer>()));

        // PollWorker is created through the DI resolver and takes the domain set directly.
        services.AddSingleton<IReadOnlySet<DomainKind>>(static sp => sp.GetRequiredService<FetchPlan>().Domains);
    }
}
