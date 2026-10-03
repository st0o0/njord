using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Mqtt.Presentation;
using Njord.Mqtt.Transport;

namespace Njord.Mqtt;

public static class MqttServiceCollectionExtensions
{
    public static IServiceCollection AddNjordMqtt(this IServiceCollection services, bool mqttEnabled)
    {
        services.AddSingleton<IEnrichmentPresenter, ConsensusPresenter>();
        services.AddSingleton<IEnrichmentPresenter, AlertPresenter>();
        services.AddSingleton<IEnrichmentPresenter, DerivedPresenter>();
        services.AddSingleton<IEnrichmentPresenter, TrendPresenter>();
        services.AddSingleton<IEnrichmentPresenter, IndexPresenter>();
        services.AddSingleton<IEnrichmentPresenter, HistoryPresenter>();

        if (mqttEnabled)
        {
            services.TryAddSingleton(MqttEgressTuning.Default);
            services.TryAddSingleton(static provider =>
                new MqttNetPublisher(
                    provider.GetRequiredService<IOptions<NjordOptions>>().Value.Mqtt,
                    provider.GetRequiredService<ILogger<MqttNetPublisher>>()));
            services.TryAddSingleton<IMqttConnection>(static provider => provider.GetRequiredService<MqttNetPublisher>());
            services.TryAddSingleton<IMqttTransport>(static provider => provider.GetRequiredService<MqttNetPublisher>());
        }

        return services;
    }
}
