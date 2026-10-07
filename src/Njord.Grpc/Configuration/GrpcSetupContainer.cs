using Akka.Cluster.Hosting;
using Akka.Cluster.Sharding;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Actors;
using Servus.Core.Application.Startup;

namespace Njord.Grpc.Configuration;

public sealed class GrpcSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddGrpc();
        services.AddSingleton<IActorRegistration>(new GrpcActorRegistration());
    }

    private sealed class GrpcActorRegistration : IActorRegistration
    {
        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.WithShardRegion<IForecastSnapshotRegion>(
                "forecast-snapshot",
                (_, _, resolver) => entityId => resolver.Props<ForecastSnapshotActor>(entityId),
                new NjordMessageExtractor(),
                new ShardOptions
                {
                    PassivateIdleEntityAfter = TimeSpan.FromMinutes(10),
                    ShouldPassivateIdleEntities = true
                });

            builder.WithShardRegion<IEnrichmentSnapshotRegion>(
                "enrichment-snapshot",
                (_, _, resolver) => entityId => resolver.Props<EnrichmentSnapshotActor>(entityId),
                new NjordMessageExtractor(),
                new ShardOptions
                {
                    PassivateIdleEntityAfter = TimeSpan.FromMinutes(10),
                    ShouldPassivateIdleEntities = true
                });

            builder.WithSingleton<IGrpcSnapshotConsumerActor>("grpc-snapshot-consumer",
                (_, _, resolver) => resolver.Props<GrpcSnapshotConsumerActor>());
        }
    }
}
