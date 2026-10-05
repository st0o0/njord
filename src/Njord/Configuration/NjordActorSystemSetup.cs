using Akka;
using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Event;
using Akka.Hosting;
using Akka.Pattern;
using Akka.Persistence.Sql.Hosting;
using Akka.Remote.Hosting;
using LinqToDB;
using Microsoft.Extensions.Options;
using Njord.Actors;
using Njord.Egress;
using Njord.Enrichment;
using Njord.Grpc;
using Njord.Mqtt;
using Njord.Pipeline;
using Njord.Sensors;
using Servus.Akka.Startup;

namespace Njord.Configuration;

public sealed class NjordActorSystemSetup : ActorSystemSetupContainer
{
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StreamStopTimeout = TimeSpan.FromSeconds(5);
    private const double RandomFactor = 0.2;

    protected override string GetActorSystemName() => "njord";

    protected override void BuildSystem(AkkaConfigurationBuilder builder, IServiceProvider serviceProvider)
    {
        var njordOptions = serviceProvider.GetRequiredService<IOptions<NjordOptions>>().Value;
        ConfigureSystem(builder, njordOptions);
    }

    internal static AkkaConfigurationBuilder ConfigureSystem(AkkaConfigurationBuilder builder, NjordOptions njordOptions)
    {
        var persistence = njordOptions.Persistence;

        var connectionString = persistence.ConnectionString
            ?? (persistence.Provider == PersistenceProvider.Sqlite
                ? $"Data Source={Path.GetFullPath(njordOptions.PersistencePath)}"
                : throw new InvalidOperationException(
                    "PostgreSQL persistence requires a connection string — set Njord:Persistence:ConnectionString."));

        var providerName = persistence.Provider switch
        {
            PersistenceProvider.Sqlite => ProviderName.SQLiteMS,
            PersistenceProvider.PostgreSql => ProviderName.PostgreSQL,
            _ => throw new InvalidOperationException($"Unsupported persistence provider: {persistence.Provider}"),
        };

        builder
            .ConfigureLoggers(loggers =>
            {
                loggers.ClearLoggers();
                loggers.AddLoggerFactory();
            })
            .WithSqlPersistence(connectionString, providerName, autoInitialize: true)
            .WithRemoting(new RemoteOptions { HostName = "localhost", Port = 0 })
            .WithClustering();

        return WithNjordActors(builder, njordOptions.Mqtt.Enabled);
    }

    internal static AkkaConfigurationBuilder WithNjordActors(AkkaConfigurationBuilder builder, bool mqttEnabled)
    {
        if (!builder.Configuration.HasValue
            || !builder.Configuration.Value.HasPath("akka.persistence.journal.plugin"))
        {
            throw new InvalidOperationException(
                "Persistence must be configured before the actors are registered (journal plugin is not set).");
        }

        // Tier 0: ShardRegion (registered before all singletons)
        builder.WithShardRegion<IForecastHistoryRegion>(
            "forecast-history",
            (_, _, resolver) => entityId => resolver.Props<ForecastHistoryActor>(entityId),
            new NjordMessageExtractor(),
            new ShardOptions
            {
                PassivateIdleEntityAfter = TimeSpan.FromMinutes(10),
                ShouldPassivateIdleEntities = true
            });

        // Tier 0: actors with no dependencies
        builder
            .WithSingleton<IBudgetTrackerActor>("budget-tracker-supervisor",
                (_, _, resolver) => BackoffSupervisorProps(resolver.Props<BudgetTrackerActor>(), "budget-tracker"))
            .WithSingleton<ISensorHubActor>("sensor-hub",
                (_, _, resolver) => resolver.Props<SensorHubActor>());

        if (mqttEnabled)
        {
            builder.WithSingleton<IMqttConnectionActor>("mqtt-connection",
                (_, _, resolver) => resolver.Props<MqttConnectionActor>());
        }

        // Tier 1: Scheduler (BackoffSupervisor) then Pipeline (resolves ISchedulerActor)
        builder
            .WithSingleton<ISchedulerActor>("scheduler-supervisor",
                (_, _, resolver) => BackoffSupervisorProps(resolver.Props<SchedulerActor>(), "scheduler"))
            .WithSingleton<IPipelineActor>("pipeline",
                (_, _, resolver) => resolver.Props<PipelineActor>());

        // Tier 2: actors that depend on Pipeline and/or SensorHub
        builder
            .WithSingleton<IModelStateActor>("model-state",
                (_, _, resolver) => resolver.Props<ModelStateActor>())
            .WithSingleton<IEnrichmentActor>("enrichment",
                (_, _, resolver) => resolver.Props<EnrichmentActor>());

        // Tier 3: actors that depend on ModelState, Enrichment, MqttConnection
        builder
            .WithSingleton<IForecastSnapshotActor>("forecast-snapshot-supervisor",
                (_, _, resolver) => BackoffSupervisorProps(resolver.Props<ForecastSnapshotActor>(), "forecast-snapshot"))
            .WithSingleton<IEnrichmentSnapshotActor>("enrichment-snapshot-supervisor",
                (_, _, resolver) => BackoffSupervisorProps(resolver.Props<EnrichmentSnapshotActor>(), "enrichment-snapshot"))
            .WithSingleton<IGrpcSnapshotConsumerActor>("grpc-snapshot-consumer",
                (_, _, resolver) => resolver.Props<GrpcSnapshotConsumerActor>());

        if (mqttEnabled)
        {
            builder
                .WithSingleton<IMqttStateActor>("mqtt-state",
                    (_, _, resolver) => resolver.Props<MqttStateActor>())
                .WithSingleton<IMqttDiscoveryActor>("mqtt-discovery",
                    (_, _, resolver) => resolver.Props<MqttDiscoveryActor>());
        }

        // Cluster self-join and shutdown task (no actor creation)
        return builder.WithActors((system, registry) =>
        {
            var cluster = Akka.Cluster.Cluster.Get(system);
            cluster.Join(cluster.SelfAddress);

            AddStreamShutdownTask(system, registry, StreamStopTimeout);
        });
    }

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

    private static Props BackoffSupervisorProps(Props childProps, string childName)
        => BackoffSupervisor.Props(
            Backoff.OnFailure(childProps, childName, MinBackoff, MaxBackoff, RandomFactor, maxNrOfRetries: -1));
}
