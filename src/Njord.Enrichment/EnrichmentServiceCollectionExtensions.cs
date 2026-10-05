using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Analysis;
using Njord.Configuration;
using Njord.Enrichment.Features;

namespace Njord.Enrichment;

public static class EnrichmentServiceCollectionExtensions
{
    public static IServiceCollection AddNjordEnrichment(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<EnrichmentOptions>()
            .Bind(configuration.GetSection(EnrichmentOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<EnrichmentOptions>, ConsensusOptionsValidator>();
        services.AddSingleton<IValidateOptions<EnrichmentOptions>, HistoryOptionsValidator>();
        services.AddSingleton<IValidateOptions<EnrichmentOptions>, IndexOptionsValidator>();

        services.AddSingleton<ConsensusSnapshotFactory>();
        services.AddSingleton<IndexComputer>();
        services.AddSingleton<TrendComputer>();
        services.AddSingleton<DerivedResultComputer>();
        services.AddSingleton<HistoryComputer>();
        services.AddSingleton<IEnrichmentFeature, AlertEnrichment>();
        services.AddSingleton<IEnrichmentFeature, DerivedEnrichment>();
        services.AddSingleton<IEnrichmentFeature, TrendEnrichment>();
        services.AddSingleton<IEnrichmentFeature, IndexEnrichment>();
        services.AddSingleton<IEnrichmentFeature, HistoryEnrichment>();
        return services;
    }
}
