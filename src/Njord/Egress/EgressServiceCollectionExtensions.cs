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
        services.TryAddSingleton<IMqttPublisher>(static provider =>
            new MqttNetPublisher(
                provider.GetRequiredService<IOptions<NjordOptions>>().Value.Mqtt,
                provider.GetRequiredService<ILogger<MqttNetPublisher>>()));
        return services;
    }
}
