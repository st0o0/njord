using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Mqtt.Actors;
using Njord.Mqtt.Health;
using Njord.Mqtt.Transport;
using Servus.Application.Startup;

namespace Njord.Mqtt.Configuration;

public sealed class MqttSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<MqttOptions>(configuration);

        var mqttEnabled = configuration
            .GetSection(MqttOptions.SectionName)
            .Get<MqttOptions>()?.Enabled ?? false;

        if (mqttEnabled)
        {
            services.TryAddSingleton(MqttEgressTuning.Default);
            services.TryAddSingleton<MqttNetPublisher>();
            services.TryAddSingleton<IMqttConnection>(static provider => provider.GetRequiredService<MqttNetPublisher>());
            services.TryAddSingleton<IMqttTransport>(static provider => provider.GetRequiredService<MqttNetPublisher>());

            services.TryAddSingleton<MqttSessionCounter>();
            services.AddHealthChecks().AddCheck<MqttConnectionHealthCheck>("mqtt-connection", failureStatus: HealthStatus.Degraded);

            services.AddSingleton<IActorRegistration>(new MqttActorRegistration());
        }
    }

    private sealed class MqttActorRegistration : IActorRegistration
    {
        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder
                .WithSingleton<IMqttConnectionActor>("mqtt-connection",
                    (_, _, resolver) => resolver.Props<MqttConnection>())
                .WithSingleton<IMqttStatePublisher>("mqtt-state",
                    (_, _, resolver) => resolver.Props<MqttStatePublisher>());
        }
    }
}
