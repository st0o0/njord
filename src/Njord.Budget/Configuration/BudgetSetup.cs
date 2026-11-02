using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Budget.Health;
using Njord.Core;
using Njord.Core.Actors;
using Terp.Akka;
using Terp.Akka.Hosting;
using Terp.Hosting;

namespace Njord.Budget.Configuration;

public sealed class BudgetSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context)
    {
        services.AddActorRegistration<BudgetActorRegistration>();
        services.AddHealthChecks().AddCheck<BudgetHealthCheck>("budget", failureStatus: HealthStatus.Unhealthy);
    }
}

internal sealed class BudgetActorRegistration : IActorRegistration
{
    public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.WithSingleton<IBudgetTracker>("budget-tracker-supervisor",
            (_, _, resolver) => resolver.Props<BudgetTracker>().BackoffOnFailure("gateway",
                SupervisorDefaults.MinBackoff, SupervisorDefaults.MaxBackoff, SupervisorDefaults.RandomFactor,
                config => config.WithMaxNrOfRetries(-1)));
    }
}
