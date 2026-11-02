using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Core;
using Njord.Core.Actors;
using Terp.Akka;
using Terp.Hosting;

namespace Njord.Grpc.Configuration;

public sealed class GrpcSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context)
    {
        services.AddGrpc();
        services.AddSingleton<IActorRegistration>(new GrpcActorRegistration());
    }

    private sealed class GrpcActorRegistration : IActorRegistration
    {
        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.WithSingleton<IGrpcConsumer>("grpc-consumer-supervisor",
                (_, _, resolver) => resolver.Props<GrpcConsumer>().BackoffOnFailure("grpc-consumer",
                    SupervisorDefaults.MinBackoff, SupervisorDefaults.MaxBackoff, SupervisorDefaults.RandomFactor,
                    config => config.WithMaxNrOfRetries(-1)));
        }
    }
}
