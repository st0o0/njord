using Njord.Alerts;
using Njord.Alerts.Configuration;
using Njord.Consensus;
using Njord.Core.Configuration;
using Njord.Derived;
using Njord.Derived.Configuration;
using Njord.Indices;
using Njord.Indices.Configuration;
using Njord.Trends;
using Njord.Trends.Configuration;
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
