using Akka.Hosting;
using Akka.Persistence.Sql.Hosting;
using LinqToDB;
using Microsoft.Extensions.Options;
using Njord.Egress;
using Njord.Mqtt;
using Njord.Enrichment;
using Njord.Domain.Analysis;
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

        var providerName = persistence.Provider switch
        {
            PersistenceProvider.Sqlite => ProviderName.SQLite,
            PersistenceProvider.PostgreSql => ProviderName.PostgreSQL,
            _ => throw new InvalidOperationException($"Unsupported persistence provider: {persistence.Provider}"),
        };

        builder
            .WithSqlPersistence(connectionString, providerName, autoInitialize: true)
            .WithResolvableActors(r =>
            {
                r.Register<MqttEgressActor>("mqtt-egress");
                r.Register<PipelineActor>("pipeline");
                r.Register<SchedulerActor>("scheduler");
                r.Register<EnrichmentActor>("enrichment");
            });
    }
}
