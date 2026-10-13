using Njord.Alerts;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
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

        services.AddAlerts();
        services.AddDerived();
        services.AddTrends();
        services.AddIndices();
    }
}
