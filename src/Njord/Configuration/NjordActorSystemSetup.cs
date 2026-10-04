using Akka;
using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Cluster.Sharding;
using Akka.DependencyInjection;
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

        builder.WithShardRegion<IForecastHistoryRegion>(
            "forecast-history",
            (_, _, resolver) => entityId => resolver.Props<ForecastHistoryActor>(entityId),
            new NjordMessageExtractor(),
            new ShardOptions
            {
                PassivateIdleEntityAfter = TimeSpan.FromMinutes(10),
                ShouldPassivateIdleEntities = true
            });

        return builder.WithActors((system, registry) =>
        {
            var cluster = Akka.Cluster.Cluster.Get(system);
            cluster.Join(cluster.SelfAddress);

            var resolver = DependencyResolver.For(system);

            RegisterPipelineActors(system, registry, resolver);
            RegisterModelStateActors(system, registry, resolver);
            RegisterEnrichmentActors(system, registry, resolver);
            RegisterSensorActors(system, registry, resolver);
            RegisterGrpcActors(system, registry, resolver);

            if (mqttEnabled)
            {
                RegisterMqttActors(system, registry, resolver);
            }

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

    private static void RegisterPipelineActors(ActorSystem system, IActorRegistry registry, DependencyResolver resolver)
    {
        RegisterWithBackoff<ISchedulerActor, SchedulerActor>(system, registry, resolver, "scheduler");
        RegisterWithBackoff<IBudgetTrackerActor, BudgetTrackerActor>(system, registry, resolver, "budget-tracker");
        registry.Register<IPipelineActor>(system.ActorOf(resolver.Props<PipelineActor>(), "pipeline"));
    }

    private static void RegisterModelStateActors(ActorSystem system, IActorRegistry registry, DependencyResolver resolver)
    {
        registry.Register<IModelStateActor>(system.ActorOf(resolver.Props<ModelStateActor>(), "model-state"));
    }

    private static void RegisterEnrichmentActors(ActorSystem system, IActorRegistry registry, DependencyResolver resolver)
    {
        registry.Register<IEnrichmentActor>(system.ActorOf(resolver.Props<EnrichmentActor>(), "enrichment"));
    }

    private static void RegisterSensorActors(ActorSystem system, IActorRegistry registry, DependencyResolver resolver)
    {
        registry.Register<ISensorHubActor>(system.ActorOf(resolver.Props<SensorHubActor>(), "sensor-hub"));
    }

    private static void RegisterGrpcActors(ActorSystem system, IActorRegistry registry, DependencyResolver resolver)
    {
        RegisterWithBackoff<IForecastSnapshotActor, ForecastSnapshotActor>(system, registry, resolver, "forecast-snapshot");
        RegisterWithBackoff<IEnrichmentSnapshotActor, EnrichmentSnapshotActor>(system, registry, resolver, "enrichment-snapshot");
        registry.Register<IGrpcSnapshotConsumerActor>(
            system.ActorOf(resolver.Props<GrpcSnapshotConsumerActor>(), "grpc-snapshot-consumer"));
    }

    private static void RegisterMqttActors(ActorSystem system, IActorRegistry registry, DependencyResolver resolver)
    {
        registry.Register<IMqttConnectionActor>(system.ActorOf(resolver.Props<MqttConnectionActor>(), "mqtt-connection"));
        registry.Register<IMqttStateActor>(system.ActorOf(resolver.Props<MqttStateActor>(), "mqtt-state"));
        registry.Register<IMqttDiscoveryActor>(system.ActorOf(resolver.Props<MqttDiscoveryActor>(), "mqtt-discovery"));
    }

    private static void RegisterWithBackoff<TKey, TActor>(
        ActorSystem system, IActorRegistry registry,
        DependencyResolver resolver, string name)
        where TActor : ActorBase
    {
        var childProps = resolver.Props<TActor>();
        var supervisorProps = BackoffSupervisor.Props(
            Backoff.OnFailure(childProps, name, MinBackoff, MaxBackoff, RandomFactor, maxNrOfRetries: -1));
        var supervisor = system.ActorOf(supervisorProps, $"{name}-supervisor");
        registry.Register<TKey>(supervisor);
    }
}
