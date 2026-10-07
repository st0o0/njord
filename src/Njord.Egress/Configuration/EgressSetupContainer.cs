using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Actors;
using Servus.Core.Application.Startup;

namespace Njord.Egress.Configuration;

public sealed class EgressSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IActorRegistration>(new EgressActorRegistration());
    }

    private sealed class EgressActorRegistration : IActorRegistration
    {
        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.WithSingleton<IModelStateActor>("model-state",
                (_, _, resolver) => resolver.Props<ModelStateActor>());
        }
    }
}
