using Microsoft.Extensions.DependencyInjection;
using Njord.Analysis;
using Njord.Enrichment.Features;

namespace Njord.Enrichment;

public static class EnrichmentServiceCollectionExtensions
{
    public static IServiceCollection AddNjordEnrichment(this IServiceCollection services)
    {
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
