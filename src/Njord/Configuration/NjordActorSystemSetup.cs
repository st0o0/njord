using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using Akka.Pattern;
using Akka.Persistence.Sql.Hosting;
using LinqToDB;
using Microsoft.Extensions.Options;
using Njord.Egress;
using Njord.Grpc;
using Njord.Mqtt;
using Njord.Enrichment;
using Njord.Pipeline;
using Servus.Akka;
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
            .WithSqlPersistence(connectionString, providerName, autoInitialize: true)
            .WithActors((system, registry) =>
            {
                var resolver = DependencyResolver.For(system);

                RegisterWithBackoff<SchedulerActor>(system, registry, resolver, "scheduler");
                RegisterWithBackoff<BudgetTrackerActor>(system, registry, resolver, "budget-tracker");
                RegisterWithBackoff<ForecastSnapshotActor>(system, registry, resolver, "forecast-snapshot");
                RegisterWithBackoff<EnrichmentSnapshotActor>(system, registry, resolver, "enrichment-snapshot");
            })
            .WithResolvableActors(r =>
            {
                r.Register<EgressActor>("egress");
                r.Register<ModelStateActor>("model-state");
                r.Register<PipelineActor>("pipeline");
                r.Register<EnrichmentActor>("enrichment");
                r.Register<GrpcSnapshotConsumerActor>("grpc-snapshot-consumer");

                if (njordOptions.Mqtt.Enabled)
                {
                    r.Register<MqttConnectionActor>("mqtt-connection");
                    r.Register<MqttEgressActor>("mqtt-egress");
                    r.Register<DiscoveryActor>("mqtt-discovery");
                }
            });
    }

    private static void RegisterWithBackoff<TActor>(
        ActorSystem system, IActorRegistry registry,
        DependencyResolver resolver, string name)
        where TActor : ActorBase
    {
        var childProps = resolver.Props<TActor>();
        var supervisorProps = BackoffSupervisor.Props(
            Backoff.OnFailure(childProps, name, MinBackoff, MaxBackoff, RandomFactor));
        var supervisor = system.ActorOf(supervisorProps, $"{name}-supervisor");
        registry.Register<TActor>(supervisor);
    }
}
