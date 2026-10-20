using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Polling;
using Servus.Akka;
using Servus.Application.Startup;

namespace Njord.Polling.Configuration;

public sealed class PollingSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<PollingOptions, PollingOptionsValidator>();

        services.TryAddSingleton(FetchTimeouts.Default);

        services.AddSingleton<IActorRegistration>(new PollingActorRegistration());
    }

    private sealed class PollingActorRegistration : IActorRegistration
    {
        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider) => builder.WithPolling();
    }
}
