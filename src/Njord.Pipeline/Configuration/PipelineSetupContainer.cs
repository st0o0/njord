using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Pattern;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Trends;
using Servus.Akka;
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

        // The settings the pipeline starts with; later changes arrive as AnalysisSettingsChanged messages.
        services.AddSingleton(static sp => AnalysisSettingsFactory.Create(
            sp.GetRequiredService<IOptions<NjordOptions>>().Value,
            sp.GetRequiredService<IOptions<ConsensusOptions>>().Value,
            sp.GetRequiredService<IOptions<AlertOptions>>().Value,
            sp.GetRequiredService<IOptions<IndexOptions>>().Value,
            sp.GetRequiredService<IOptions<TrendOptions>>().Value,
            sp.GetRequiredService<IOptions<DerivedOptions>>().Value));

        services.TryAddSingleton(FetchTimeouts.Default);

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
                    (_, _, resolver) => resolver.Props<ForecastPipeline>().BackoffOnFailure("pipeline",
                        MinBackoff, MaxBackoff, RandomFactor, config => config.WithMaxNrOfRetries(-1)))
                .WithSingleton<IForecastReadModel>("forecast-read-model",
                    (_, _, resolver) => resolver.Props<ForecastReadModel>());
        }
    }
}
