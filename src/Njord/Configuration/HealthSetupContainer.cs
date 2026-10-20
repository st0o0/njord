using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Health;
using Njord.Health;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class HealthSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<HealthOptions, HealthOptionsValidator>();
        services.AddRequestTimeouts();

        services.AddHealthChecks()
            .AddCheck<JournalHealthCheck>("journal", failureStatus: HealthStatus.Unhealthy);
    }
}
