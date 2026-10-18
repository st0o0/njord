using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Polling;
using Njord.Core.Health;
using Njord.Health;
using Servus.Application.Startup;

namespace Njord.Configuration;

public sealed class HealthSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(static sp => new NjordHealthMonitor(
            sp.GetRequiredService<TimeProvider>().GetUtcNow(),
            sp.GetRequiredService<IOptionsMonitor<PollingOptions>>().CurrentValue.ToInitialBudgetHealth()));

        services.AddHealthChecks()
            .AddCheck<PipelineHealthCheck>("pipeline");
    }
}
