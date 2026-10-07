using Akka;
using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Event;
using Akka.Hosting;
using Akka.Pattern;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Actors;
using Njord.Messages.Pipeline;
using Servus.Core.Application.Startup;

namespace Njord.Pipeline.Configuration;

public sealed class PipelineSetupContainer : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IBudgetProvider, OptionsBudgetProvider>();
        services.AddSingleton<IBudgetGate<WeightedTarget>>(sp =>
            new WeightedBudgetGate(
                sp.GetRequiredService<IBudgetProvider>(),
                sp.GetRequiredService<ActorRegistry>().Get<IBudgetTrackerActor>(),
                sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IActorRegistration>(new PipelineActorRegistration());
    }

    private sealed class PipelineActorRegistration : IActorRegistration
    {
        private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan StreamStopTimeout = TimeSpan.FromSeconds(5);
        private const double RandomFactor = 0.2;

        public void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder
                .WithSingleton<ISchedulerActor>("scheduler-supervisor",
                    (_, _, resolver) => BackoffSupervisorProps(resolver.Props<SchedulerActor>(), "scheduler"))
                .WithSingleton<IBudgetTrackerActor>("budget-tracker-supervisor",
                    (_, _, resolver) => BackoffSupervisorProps(resolver.Props<BudgetTrackerActor>(), "budget-tracker"))
                .WithSingleton<IPipelineActor>("pipeline",
                    (_, _, resolver) => resolver.Props<PipelineActor>());

            builder.WithActors((system, registry) =>
            {
                AddStreamShutdownTask(system, registry, StreamStopTimeout);
            });
        }

        private static Props BackoffSupervisorProps(Props childProps, string childName)
            => BackoffSupervisor.Props(
                Backoff.OnFailure(childProps, childName, MinBackoff, MaxBackoff, RandomFactor, maxNrOfRetries: -1));

        internal static void AddStreamShutdownTask(ActorSystem system, IActorRegistry registry, TimeSpan askTimeout)
        {
            var log = Logging.GetLogger(system, "NjordStreamShutdown");

            CoordinatedShutdown.Get(system).AddTask(
                CoordinatedShutdown.PhaseBeforeServiceUnbind,
                "stop-njord-streams",
                async () =>
                {
                    await StopStreamsOf<IPipelineActor>(registry, askTimeout, log);
                    return Done.Instance;
                });
        }

        private static async Task StopStreamsOf<TKey>(IActorRegistry registry, TimeSpan askTimeout, ILoggingAdapter log)
        {
            var name = typeof(TKey).Name;
            try
            {
                var actor = await registry.GetAsync<TKey>();
                var reply = await actor.Ask<object>(new StopStreams(), askTimeout);
                if (reply is StreamsStopFailed failed)
                {
                    log.Warning(failed.Cause, "Failed to stop streams of {0}", name);
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Failed to stop streams of {0}", name);
            }
        }
    }
}
