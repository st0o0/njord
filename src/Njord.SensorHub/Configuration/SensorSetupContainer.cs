using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Servus.Core.Application.Startup;

namespace Njord.SensorHub.Configuration;

public sealed class SensorSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<SensorOptions>()
            .BindConfiguration(SensorOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SensorOptions>, SensorOptionsValidator>();

        services.AddSingleton<IActorRegistration>(new SensorActorRegistration());
    }

    private sealed class SensorActorRegistration : IActorRegistration
    {
        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.WithSingleton<ISensorHub>("sensor-hub",
                (_, _, resolver) => resolver.Props<SensorHub>());
        }
    }
}
