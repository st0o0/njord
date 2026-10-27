using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Features;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class DomainSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddFeatures();

        services.AddOptions<FeatureRequirementsOptions>()
            .Configure<IEnumerable<IFeatureRule>, IOptions<ForecastOptions>>(
                static (options, features, weather) =>
                    FeatureResolution.Configure(options, features, weather.Value))
            .ValidateOnStart();
    }
}
