using Microsoft.Extensions.Configuration;
using Njord.Alerts;
using Njord.Derived;
using Njord.History;
using Njord.Indices;
using Njord.Trends;
using Servus.Core.Application.Startup;

namespace Njord.Configuration;

public sealed class AnalysisSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAlerts();
        services.AddDerived();
        services.AddTrends();
        services.AddIndices();
        services.AddHistory();
    }
}
