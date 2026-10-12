using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Compute.Analysis;
using Njord.Compute.Configuration;
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
        var enrichmentSection = configuration.GetSection(EnrichmentOptions.SectionName);

        services
            .AddOptions<EnrichmentOptions>()
            .Bind(enrichmentSection)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<EnrichmentOptions>, ConsensusOptionsValidator>();
        services.AddSingleton<IValidateOptions<EnrichmentOptions>, IndexOptionsValidator>();

        services.AddOptions<AlertOptions>().Bind(enrichmentSection.GetSection("Alerts"));
        services.AddOptions<ConsensusOptions>().Bind(enrichmentSection.GetSection("Consensus"));
        services.AddOptions<DerivedOptions>().Bind(enrichmentSection.GetSection("Derived"));
        services.AddOptions<IndexOptions>().Bind(enrichmentSection.GetSection("Indices"));
        services.AddOptions<TrendOptions>().Bind(enrichmentSection.GetSection("Trends"));

        services.AddSingleton<ConsensusSnapshotFactory>();

        services.AddAlerts();
        services.AddDerived();
        services.AddTrends();
        services.AddIndices();
    }
}
