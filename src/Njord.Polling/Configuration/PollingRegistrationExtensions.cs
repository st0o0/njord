using Akka.Cluster.Hosting;
using Akka.Hosting;
using Njord.Core;
using Njord.Core.Actors;
using Servus.Akka;

namespace Njord.Polling.Configuration;

// The poll shard region (one persistent worker per target, never passivated, not remembered: the manager starts
// the configured set) and the manager singleton that orchestrates it. The region is registered first.
internal static class PollingRegistrationExtensions
{
    internal static AkkaConfigurationBuilder WithPolling(this AkkaConfigurationBuilder builder) => builder
        .WithShardRegion<IPollRegion>("poll-worker",
            (_, _, resolver) => entityId => resolver.Props<PollWorker>(entityId),
            new ShardMessageExtractor(),
            new ShardOptions { ShouldPassivateIdleEntities = false, RememberEntities = false })
        .WithSingleton<IPollManager>("poll-manager",
            (_, _, resolver) => resolver.Props<PollManager>().BackoffOnFailure("poll-manager",
                SupervisorDefaults.MinBackoff, SupervisorDefaults.MaxBackoff, SupervisorDefaults.RandomFactor,
                config => config.WithMaxNrOfRetries(-1)));
}
