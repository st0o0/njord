using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;
using Njord.Derived;
using Njord.Indices;
using Njord.Trends;
using Servus.Core.Application.Startup;

namespace Njord.Configuration;

public sealed class AnalysisSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<EnrichmentOptions>()
            .Bind(configuration.GetSection(EnrichmentOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<EnrichmentOptions>, ConsensusOptionsValidator>();
        services.AddSingleton<IValidateOptions<EnrichmentOptions>, IndexOptionsValidator>();

        services.AddSingleton<ConsensusSnapshotFactory>();

        services.AddAlerts();
        services.AddDerived();
        services.AddTrends();
        services.AddIndices();
    }
}
