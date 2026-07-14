using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Njord.Domain.Weather;
using Njord.Health;
using Njord.Ingest;
using Njord.Mqtt;
using Njord.Mqtt.Transport;
using Servus.Core.Application.Startup;

namespace Njord.Configuration;

public sealed class NjordServiceSetup : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<NjordOptions>()
            .Bind(configuration.GetSection(NjordOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<NjordOptions>, NjordOptionsValidator>();
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NjordOptions>>().Value;
            return ParameterRegistry.Resolve(
                options.Parameters.Groups,
                options.Parameters.Extra,
                options.Parameters.Exclude);
        });
        services
            .AddOptions<EnrichmentOptions>()
            .Bind(configuration.GetSection($"{NjordOptions.SectionName}:Enrichment"));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new NjordHealthState
        {
            ServiceStartedUtc = sp.GetRequiredService<TimeProvider>().GetUtcNow(),
        });
        services.AddHealthChecks()
            .AddCheck<MqttConnectionHealthCheck>("mqtt-connection")
            .AddCheck<PipelineHealthCheck>("pipeline");
        services.AddOpenMeteoIngest();
        services.TryAddSingleton(MqttEgressTuning.Default);
        services.TryAddSingleton(static provider =>
            new MqttNetPublisher(
                provider.GetRequiredService<IOptions<NjordOptions>>().Value.Mqtt,
                provider.GetRequiredService<ILogger<MqttNetPublisher>>()));
        services.TryAddSingleton<IMqttConnection>(static provider => provider.GetRequiredService<MqttNetPublisher>());
        services.TryAddSingleton<IMqttTransport>(static provider => provider.GetRequiredService<MqttNetPublisher>());
    }
}
