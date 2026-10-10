using Akka.Cluster.Hosting;
using Akka.Cluster.Sharding;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Compute.Analysis;
using Njord.Core.Actors;
using Njord.Core.Configuration;
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
        services
            .AddOptions<EnrichmentOptions>()
            .Bind(configuration.GetSection(EnrichmentOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<EnrichmentOptions>, ConsensusOptionsValidator>();
        services.AddSingleton<IValidateOptions<EnrichmentOptions>, HistoryOptionsValidator>();
        services.AddSingleton<IValidateOptions<EnrichmentOptions>, IndexOptionsValidator>();

        services.AddSingleton<ConsensusSnapshotFactory>();

        services.AddAlerts();
        services.AddDerived();
        services.AddTrends();
        services.AddIndices();
        services.AddHistory();

        services.AddSingleton<IActorRegistration>(new HistoryActorRegistration());
    }

    private sealed class HistoryActorRegistration : IActorRegistration
    {
        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
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
