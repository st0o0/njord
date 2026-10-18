using Njord.Alerts;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Trends;
using Njord.Derived;
using Njord.Indices;
using Njord.Trends;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class AnalysisSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<AlertOptions>(configuration);
        services.AddNjordOptions<DerivedOptions>(configuration);
        services.AddNjordOptions<TrendOptions>(configuration);
        services.AddNjordOptions<IndexOptions>(configuration);

        services.AddSingleton<ConsensusBuilder>();

        services.AddAlerts();
        services.AddDerived();
        services.AddTrends();
        services.AddIndices();
    }
}
