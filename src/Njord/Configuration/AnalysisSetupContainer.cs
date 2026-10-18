using Njord.Alerts;
using Njord.Consensus;
using Njord.Derived;
using Njord.Indices;
using Njord.Trends;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class AnalysisSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {

        services.AddSingleton<ConsensusBuilder>();

        services.AddAlerts(configuration);
        services.AddDerived(configuration);
        services.AddTrends(configuration);
        services.AddIndices(configuration);
    }
}
