using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Mqtt.Transport;
using Servus.Core.Application.Startup;

namespace Njord.Mqtt.Configuration;

public sealed class MqttSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<MqttOptions>()
            .Bind(configuration.GetSection(MqttOptions.SectionName))
            .ValidateOnStart();

        var mqttEnabled = configuration
            .GetSection($"{NjordOptions.SectionName}:Mqtt")
            .GetValue("Enabled", false);

        if (mqttEnabled)
        {
            services.TryAddSingleton(MqttEgressTuning.Default);
            services.TryAddSingleton(static provider =>
                new MqttNetPublisher(
                    provider.GetRequiredService<IOptions<MqttOptions>>().Value,
                    provider.GetRequiredService<ILogger<MqttNetPublisher>>()));
            services.TryAddSingleton<IMqttConnection>(static provider => provider.GetRequiredService<MqttNetPublisher>());
            services.TryAddSingleton<IMqttTransport>(static provider => provider.GetRequiredService<MqttNetPublisher>());

            services.AddSingleton<IActorRegistration>(new MqttActorRegistration());
        }
    }

    private sealed class MqttActorRegistration : IActorRegistration
    {
        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder
                .WithSingleton<IMqttConnectionActor>("mqtt-connection",
                    (_, _, resolver) => resolver.Props<MqttConnectionActor>())
                .WithSingleton<IMqttStateActor>("mqtt-state",
                    (_, _, resolver) => resolver.Props<MqttStateActor>());
        }
    }
}
