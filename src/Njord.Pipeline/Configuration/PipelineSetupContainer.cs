using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Pattern;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Actors;
using Servus.Core.Application.Startup;

namespace Njord.Pipeline.Configuration;

public sealed class PipelineSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IActorRegistration>(new PipelineActorRegistration());
    }

    private sealed class PipelineActorRegistration : IActorRegistration
    {
        private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
        private const double RandomFactor = 0.2;

        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder
                .WithSingleton<IPollScheduler>("scheduler-supervisor",
                    (_, _, resolver) => BackoffSupervisor.Props(
                        Backoff.OnFailure(resolver.Props<PollScheduler>(), "scheduler",
                            MinBackoff, MaxBackoff, RandomFactor, maxNrOfRetries: -1)))
                .WithSingleton<IForecastPipeline>("pipeline",
                    (_, _, resolver) => resolver.Props<ForecastPipeline>());
        }
    }
}
