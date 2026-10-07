using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Actors;
using Servus.Core.Application.Startup;

namespace Njord.Sensors.Configuration;

public sealed class SensorSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IActorRegistration>(new SensorActorRegistration());
    }

    private sealed class SensorActorRegistration : IActorRegistration
    {
        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.WithSingleton<ISensorHubActor>("sensor-hub",
                (_, _, resolver) => resolver.Props<SensorHubActor>());
        }
    }
}
