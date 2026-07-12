using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Configuration;

namespace Njord.Egress;

public static class EgressServiceCollectionExtensions
{
    public static IServiceCollection AddMqttEgress(this IServiceCollection services)
    {
        services.TryAddSingleton(MqttEgressTuning.Default);
        services.TryAddSingleton(static provider =>
            new MqttNetPublisher(
                provider.GetRequiredService<IOptions<NjordOptions>>().Value.Mqtt,
                provider.GetRequiredService<ILogger<MqttNetPublisher>>()));
        services.TryAddSingleton<IMqttConnection>(static provider => provider.GetRequiredService<MqttNetPublisher>());
        services.TryAddSingleton<IMqttTransport>(static provider => provider.GetRequiredService<MqttNetPublisher>());
        return services;
    }
}
