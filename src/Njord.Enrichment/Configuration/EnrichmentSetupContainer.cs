using Akka.Cluster.Hosting;
using Akka.Cluster.Sharding;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;
using Njord.Enrichment.Features;
using Servus.Core.Application.Startup;

namespace Njord.Enrichment.Configuration;

public sealed class EnrichmentSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
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

        services.AddSingleton<IActorRegistration>(new EnrichmentActorRegistration());
    }

    private sealed class EnrichmentActorRegistration : IActorRegistration
    {
        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.WithSingleton<IEnrichmentActor>("enrichment",
                (_, _, resolver) => resolver.Props<EnrichmentActor>());

            builder.WithShardRegion<IForecastHistoryRegion>(
                "forecast-history",
                (_, _, resolver) => entityId => resolver.Props<ForecastHistoryActor>(entityId),
                new NjordMessageExtractor(),
                new ShardOptions
                {
                    PassivateIdleEntityAfter = TimeSpan.FromMinutes(10),
                    ShouldPassivateIdleEntities = true
                });
        }
    }
}
