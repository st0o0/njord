using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Configuration;
using Njord.Core.Polling;
using Terp.Akka.Hosting;
using Terp.Hosting;

namespace Njord.Polling.Configuration;

public sealed class PollingSetup : ISetup<IServiceCollection>
{
    public void Setup(IServiceCollection services, SetupContext context)
    {
        services.AddNjordOptions<PollingOptions, PollingOptionsValidator>();
        services.PostConfigure<PollingOptions>(options => options.Budget = options.BudgetOverride ?? RequestBudget.OpenMeteoFreeTier);

        services.AddActorRegistration<PollingActorRegistration>();
    }
}

internal sealed class PollingActorRegistration : IActorRegistration
{
    public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider) => builder.WithPolling();
}
