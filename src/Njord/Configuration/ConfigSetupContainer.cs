using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Persistence;
using Njord.Core.Configuration.Polling;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class ConfigSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<NjordOptions>(configuration);
        services.AddNjordOptions<EnrichmentOptions>(configuration);
        services.AddNjordOptions<PersistenceOptions>(configuration);

        services.AddSingleton<ConfigView>();
        services.AddSingleton(static sp => ServiceInfoMappings.ToServiceInfo(
            sp.GetRequiredService<IOptions<NjordOptions>>().Value,
            sp.GetRequiredService<IOptions<PollingOptions>>().Value,
            sp.GetRequiredService<IOptions<AlertOptions>>().Value,
            sp.GetRequiredService<IOptions<EnrichmentOptions>>().Value));
    }
}
