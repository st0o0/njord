using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Compute.Analysis;
using Njord.Compute.Configuration;
using Njord.Core.Configuration;
using Njord.Derived;
using Njord.Indices;
using Njord.Trends;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class AnalysisSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<EnrichmentOptions>()
            .BindConfiguration(EnrichmentOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<EnrichmentOptions>, ConsensusOptionsValidator>();
        services.AddSingleton<IValidateOptions<EnrichmentOptions>, IndexOptionsValidator>();

        services.AddOptions<AlertOptions>().BindConfiguration($"{EnrichmentOptions.SectionName}:Alerts");
        services.AddOptions<ConsensusOptions>().BindConfiguration($"{EnrichmentOptions.SectionName}:Consensus");
        services.AddOptions<DerivedOptions>().BindConfiguration($"{EnrichmentOptions.SectionName}:Derived");
        services.AddOptions<IndexOptions>().BindConfiguration($"{EnrichmentOptions.SectionName}:Indices");
        services.AddOptions<TrendOptions>().BindConfiguration($"{EnrichmentOptions.SectionName}:Trends");

        services.AddSingleton<ConsensusSnapshotFactory>();

        services.AddAlerts();
        services.AddDerived();
        services.AddTrends();
        services.AddIndices();
    }
}
