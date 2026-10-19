using Microsoft.Extensions.Options;
using Njord.Alerts.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Persistence;
using Njord.Core.Configuration.Polling;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Pipeline.Configuration;
using Njord.Trends.Configuration;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class ConfigSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<NjordOptions>(configuration);
        services.AddNjordOptions<PersistenceOptions>(configuration);
        services.AddNjordOptions<StorageOptions>(configuration);

        services.AddSingleton(static sp => sp.GetRequiredService<IOptions<NjordOptions>>().Value.Parameters.Resolve());

        services.AddSingleton<ConfigView>();
        services.AddSingleton(static sp => ServiceInfoMappings.ToServiceInfo(
            sp.GetRequiredService<IOptions<NjordOptions>>().Value,
            sp.GetRequiredService<IOptions<PollingOptions>>().Value,
            sp.GetRequiredService<IOptions<ConsensusOptions>>().Value,
            sp.GetRequiredService<IOptions<AlertOptions>>().Value,
            sp.GetRequiredService<IOptions<DerivedOptions>>().Value,
            sp.GetRequiredService<IOptions<TrendOptions>>().Value,
            sp.GetRequiredService<IOptions<IndexOptions>>().Value));
    }
}
