using Akka.Hosting;
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
    protected override string GetActorSystemName() => "njord";

    protected override void BuildSystem(AkkaConfigurationBuilder builder, IServiceProvider serviceProvider)
    {
        var njordOptions = serviceProvider.GetRequiredService<IOptions<NjordOptions>>().Value;
        var persistence = njordOptions.Persistence;

        var connectionString = persistence.ConnectionString
            ?? (persistence.Provider == PersistenceProvider.Sqlite
                ? $"Data Source={njordOptions.PersistencePath}"
                : throw new InvalidOperationException(
                    "PostgreSQL persistence requires a connection string — set Njord:Persistence:ConnectionString."));

        if (persistence is { Provider: PersistenceProvider.Sqlite, ConnectionString: null })
        {
            var dir = Path.GetDirectoryName(njordOptions.PersistencePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

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
            .WithResolvableActors(r =>
            {
                r.Register<EgressActor>("egress");
                r.Register<ModelStateActor>("model-state");
                r.Register<PipelineActor>("pipeline");
                r.Register<SchedulerActor>("scheduler");
                r.Register<EnrichmentActor>("enrichment");
                r.Register<ForecastSnapshotActor>("forecast-snapshot");
                r.Register<EnrichmentSnapshotActor>("enrichment-snapshot");
                r.Register<GrpcSnapshotConsumerActor>("grpc-snapshot-consumer");

                if (njordOptions.Mqtt.Enabled)
                {
                    r.Register<MqttConnectionActor>("mqtt-connection");
                    r.Register<MqttEgressActor>("mqtt-egress");
                    r.Register<DiscoveryActor>("mqtt-discovery");
                }
            });
    }
}
