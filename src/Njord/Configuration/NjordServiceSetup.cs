using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Njord.Domain.Weather;
using Njord.Enrichment;
using Njord.Enrichment.Features;
using Njord.Grpc;
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
        var healthChecks = services.AddHealthChecks()
            .AddCheck<PipelineHealthCheck>("pipeline");

        var mqttEnabled = configuration
            .GetSection($"{NjordOptions.SectionName}:Mqtt")
            .GetValue("Enabled", true);
        services.AddSingleton<IEnrichmentFeature, ConsensusEnrichment>();
        services.AddSingleton<IEnrichmentFeature, AlertEnrichment>();
        services.AddSingleton<IEnrichmentFeature, DerivedEnrichment>();
        services.AddSingleton<IEnrichmentFeature, TrendEnrichment>();
        services.AddSingleton<IEnrichmentFeature, IndexEnrichment>();
        services.AddSingleton<IEnrichmentFeature, EnergyEnrichment>();
        services.AddSingleton<IEnrichmentFeature, HistoryEnrichment>();
        if (mqttEnabled)
        {
            healthChecks.AddCheck<MqttConnectionHealthCheck>("mqtt-connection");
            services.TryAddSingleton(MqttEgressTuning.Default);
            services.TryAddSingleton(static provider =>
                new MqttNetPublisher(
                    provider.GetRequiredService<IOptions<NjordOptions>>().Value.Mqtt,
                    provider.GetRequiredService<ILogger<MqttNetPublisher>>()));
            services.TryAddSingleton<IMqttConnection>(static provider => provider.GetRequiredService<MqttNetPublisher>());
            services.TryAddSingleton<IMqttTransport>(static provider => provider.GetRequiredService<MqttNetPublisher>());
        }

        services.AddGrpc();
        services.AddOpenMeteoIngest();
        services.AddSingleton<ConfigPersistence>();
        services.AddSingleton<BudgetTracker>();
    }
}
