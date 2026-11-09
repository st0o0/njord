using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Messages.Results;
using Terp.Akka;
using Terp.Akka.Hosting;
using Terp.Hosting;

namespace Njord.Grpc.Configuration;

public sealed class GrpcSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context)
    {
        services.AddGrpc();
        services.AddSingleton(sp => new ManifestBuilder(
            sp.GetServices<IFeatureRule>(),
            sp.GetRequiredService<IOptions<NjordOptions>>().Value,
            sp.GetRequiredService<IOptions<ActiveModels>>().Value));
        services.AddSingleton(sp => new EntityIdLookup(
            sp.GetServices<IFeatureRule>(),
            sp.GetRequiredService<IOptions<NjordOptions>>().Value));
        services.TryAddSingleton(TimeProvider.System);
        services.AddActorRegistration<GrpcActorRegistration>();
    }
}

internal sealed class GrpcActorRegistration : IActorRegistration
{
    public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.WithSingleton<IGrpcConsumer>("grpc-consumer-supervisor",
            (_, _, resolver) => resolver.Props<GrpcConsumer>().BackoffOnFailure("grpc-consumer",
                SupervisorDefaults.MinBackoff, SupervisorDefaults.MaxBackoff, SupervisorDefaults.RandomFactor,
                config => config.WithMaxNrOfRetries(-1)));
    }
}
