using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class ConfigSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<NjordOptions, NjordOptionsValidator>();
        services.AddNjordOptions<PersistenceOptions, PersistenceOptionsValidator>();

        services.AddSingleton(static sp => sp.GetRequiredService<IOptions<NjordOptions>>().Value.ToServiceInfo(
            sp.GetRequiredService<IOptions<PollingOptions>>().Value,
            sp.GetServices<IFeatureRule>()));
    }
}
