using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using Akka.Pattern;
using Akka.Persistence.Sql.Hosting;
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
    private const double RandomFactor = 0.2;

    protected override string GetActorSystemName() => "njord";

    protected override void BuildSystem(AkkaConfigurationBuilder builder, IServiceProvider serviceProvider)
    {
        var njordOptions = serviceProvider.GetRequiredService<IOptions<NjordOptions>>().Value;
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
            .WithSqlPersistence(connectionString, providerName, autoInitialize: true);

        WithNjordActors(builder, njordOptions.Mqtt.Enabled);
    }

    internal static AkkaConfigurationBuilder WithNjordActors(AkkaConfigurationBuilder builder, bool mqttEnabled)
    {
        return builder.WithActors((system, registry) =>
        {
            var resolver = DependencyResolver.For(system);

            RegisterPipelineActors(system, registry, resolver);
            RegisterEgressActors(system, registry, resolver);
            RegisterEnrichmentActors(system, registry, resolver);
            RegisterSensorActors(system, registry, resolver);
            RegisterGrpcActors(system, registry, resolver);

            if (mqttEnabled)
            {
                RegisterMqttActors(system, registry, resolver);
            }
        });
    }

    private static void RegisterPipelineActors(ActorSystem system, IActorRegistry registry, DependencyResolver resolver)
    {
        RegisterWithBackoff<ISchedulerActor, SchedulerActor>(system, registry, resolver, "scheduler");
        RegisterWithBackoff<IBudgetTrackerActor, BudgetTrackerActor>(system, registry, resolver, "budget-tracker");
        registry.Register<IPipelineActor>(system.ActorOf(resolver.Props<PipelineActor>(), "pipeline"));
    }

    private static void RegisterEgressActors(ActorSystem system, IActorRegistry registry, DependencyResolver resolver)
    {
        registry.Register<IEgressActor>(system.ActorOf(resolver.Props<EgressActor>(), "egress"));
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
        registry.Register<IMqttEgressActor>(system.ActorOf(resolver.Props<MqttEgressActor>(), "mqtt-egress"));
        registry.Register<IDiscoveryActor>(system.ActorOf(resolver.Props<DiscoveryActor>(), "mqtt-discovery"));
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
