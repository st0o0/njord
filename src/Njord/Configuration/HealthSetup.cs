using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Core.Configuration;
using Njord.Health;
using Terp.Hosting;

namespace Njord.Configuration;

public sealed class HealthSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context)
    {
        services.AddNjordOptions<HealthOptions, HealthOptionsValidator>();
        services.AddRequestTimeouts();

        var persistence = new PersistenceOptions();
        context.Configuration.GetSection(IConfigSection.SectionOf<PersistenceOptions>()).Bind(persistence);

        var healthChecks = services.AddHealthChecks();

        if (string.IsNullOrEmpty(persistence.ConnectionString))
        {
            healthChecks.AddCheck<DataDirectoryHealthCheck>("data-directory",
                failureStatus: HealthStatus.Unhealthy);
        }
    }
}
