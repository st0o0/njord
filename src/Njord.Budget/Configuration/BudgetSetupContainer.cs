using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Budget.Health;
using Njord.Core;
using Njord.Core.Actors;
using Servus.Akka;
using Servus.Application.Startup;

namespace Njord.Budget.Configuration;

public sealed class BudgetSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IActorRegistration>(new BudgetActorRegistration());
        services.AddHealthChecks().AddCheck<BudgetHealthCheck>("budget", failureStatus: HealthStatus.Unhealthy);
    }

    private sealed class BudgetActorRegistration : IActorRegistration
    {
        private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
        private const double RandomFactor = 0.2;

        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.WithSingleton<IBudgetTracker>("budget-tracker-supervisor",
                (_, _, resolver) => resolver.Props<BudgetTracker>().BackoffOnFailure("gateway", MinBackoff,
                    MaxBackoff, RandomFactor, config => config.WithMaxNrOfRetries(-1)));
        }
    }
}
