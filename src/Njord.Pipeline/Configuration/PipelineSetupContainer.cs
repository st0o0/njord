using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Njord.Core;
using Njord.Core.Actors;
using Njord.Pipeline.Health;
using Servus.Application.Startup;

namespace Njord.Pipeline.Configuration;

public sealed class PipelineSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks().AddCheck<PipelineHealthCheck>("pipeline", failureStatus: HealthStatus.Unhealthy);

        services.AddSingleton<IActorRegistration>(new PipelineActorRegistration());
    }

    private sealed class PipelineActorRegistration : IActorRegistration
    {
        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.WithSingleton<IForecastPipeline>("pipeline",
                (_, _, resolver) => resolver.Props<ForecastPipeline>());
        }
    }
}
