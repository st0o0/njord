using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Njord.Alerts.Configuration;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Pipeline.Health;
using Njord.Trends.Configuration;
using Servus.Akka;
using Servus.Application.Startup;

namespace Njord.Pipeline.Configuration;

public sealed class PipelineSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<PollingOptions>(configuration);
        services.AddNjordOptions<ConsensusOptions>(configuration);

        // The settings the pipeline starts with; it asks ConfigManager for the current ones right after, and later
        // changes arrive as AnalysisSettingsChanged messages.
        services.AddSingleton(static sp => AnalysisSettingsFactory.Create(
            sp.GetRequiredService<IOptions<NjordOptions>>().Value,
            sp.GetRequiredService<IOptions<ConsensusOptions>>().Value,
            sp.GetRequiredService<IOptions<AlertOptions>>().Value,
            sp.GetRequiredService<IOptions<IndexOptions>>().Value,
            sp.GetRequiredService<IOptions<TrendOptions>>().Value,
            sp.GetRequiredService<IOptions<DerivedOptions>>().Value));

        services.TryAddSingleton(FetchTimeouts.Default);

        services.AddHealthChecks().AddCheck<PipelineHealthCheck>("pipeline", failureStatus: HealthStatus.Unhealthy);

        services.AddSingleton<IActorRegistration>(new PipelineActorRegistration());
    }

    private sealed class PipelineActorRegistration : IActorRegistration
    {

        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder
                .WithPolling()
                .WithSingleton<IForecastPipeline>("pipeline",
                    (_, _, resolver) => resolver.Props<ForecastPipeline>().BackoffOnFailure("pipeline",
                        PollingRegistration.MinBackoff, PollingRegistration.MaxBackoff, PollingRegistration.RandomFactor, config => config.WithMaxNrOfRetries(-1)));
        }
    }
}

// The poll shard region (one persistent worker per target, never passivated, not remembered: the manager starts
// the configured set) and the manager singleton that orchestrates it. The region is registered first.
internal static class PollingRegistration
{
    internal static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(3);
    internal static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    internal const double RandomFactor = 0.2;

    internal static AkkaConfigurationBuilder WithPolling(this AkkaConfigurationBuilder builder) => builder
        .WithShardRegion<IPollRegion>("poll-worker",
            (_, _, resolver) => entityId => resolver.Props<PollWorker>(entityId),
            new ShardMessageExtractor(),
            new ShardOptions { ShouldPassivateIdleEntities = false, RememberEntities = false })
        .WithSingleton<IPollManager>("poll-manager",
            (_, _, resolver) => resolver.Props<PollManager>().BackoffOnFailure("poll-manager",
                MinBackoff, MaxBackoff, RandomFactor, config => config.WithMaxNrOfRetries(-1)));
}
