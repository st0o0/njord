using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Pattern;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Polling;
using Servus.Application.Startup;

namespace Njord.Pipeline.Configuration;

public sealed class PipelineSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<PollingOptions>()
            .BindConfiguration(PollingOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PollingOptions>, PollingOptionsValidator>();

        services
            .AddOptions<ConsensusOptions>()
            .BindConfiguration(ConsensusOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ConsensusOptions>, ConsensusOptionsValidator>();

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
