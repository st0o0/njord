using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Health;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class HealthSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<HealthOptions, HealthOptionsValidator>();
        services.AddRequestTimeouts();

        var persistence = new PersistenceOptions();
        configuration.GetSection(IConfigSection.SectionOf<PersistenceOptions>()).Bind(persistence);

        var healthChecks = services.AddHealthChecks();

        if (persistence.Provider == PersistenceProvider.Sqlite)
        {
            healthChecks.AddCheck<DataDirectoryHealthCheck>("data-directory",
                failureStatus: HealthStatus.Unhealthy);
        }
    }
}
