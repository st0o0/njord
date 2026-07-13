using Microsoft.Extensions.Options;
using Njord.Domain;
using Njord.Egress;
using Njord.Ingest;
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
        services.AddHealthChecks();
        services.AddOpenMeteoIngest();
        services.AddMqttEgress();
    }
}
