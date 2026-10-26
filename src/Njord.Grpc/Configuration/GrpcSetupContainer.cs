using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Core;
using Njord.Core.Actors;
using Servus.Akka;
using Servus.Application.Startup;

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
        private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
        private const double RandomFactor = 0.2;

        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.WithSingleton<IGrpcConsumer>("grpc-consumer-supervisor",
                (_, _, resolver) => resolver.Props<GrpcConsumer>().BackoffOnFailure("grpc-consumer",
                    MinBackoff, MaxBackoff, RandomFactor, config => config.WithMaxNrOfRetries(-1)));
        }
    }
}
