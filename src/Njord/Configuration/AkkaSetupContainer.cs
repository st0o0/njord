using Akka.Cluster;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Persistence.Sql.Hosting;
using Akka.Remote.Hosting;
using LinqToDB;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Servus.Akka.Startup;

namespace Njord.Configuration;

public sealed class AkkaSetupContainer : ActorSystemSetupContainer
{
    protected override string GetActorSystemName() => "njord";

    protected override void BuildSystem(AkkaConfigurationBuilder builder, IServiceProvider serviceProvider)
    {
        var persistence = serviceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;

        string connectionString;
        string providerName;

        if (!string.IsNullOrEmpty(persistence.ConnectionString))
        {
            connectionString = persistence.ConnectionString;
            providerName = ProviderName.PostgreSQL;
        }
        else
        {
            connectionString = $"Data Source={Path.GetFullPath(persistence.DataPath)}";
            providerName = ProviderName.SQLiteMS;
        }

        var port = serviceProvider.GetRequiredService<IConfiguration>().GetValue("Akka:RemotingPort", 2552);

        builder
            .ConfigureLoggers(loggers =>
            {
                loggers.ClearLoggers();
                loggers.AddLoggerFactory();
            })
            .WithSqlPersistence(connectionString, providerName, autoInitialize: true,
                journalBuilder: journal => journal.WithHealthCheck(),
                snapshotBuilder: snapshot => snapshot.WithHealthCheck())
            .WithActorSystemLivenessCheck()
            .WithAkkaClusterReadinessCheck()
            .WithRemoting(new RemoteOptions { HostName = "localhost", Port = port })
            .WithClustering(port == 0
                ? new ClusterOptions()
                : new ClusterOptions { SeedNodes = [$"akka.tcp://njord@localhost:{port}"] });

        if (port == 0)
        {
            builder.AddStartup((system, _) =>
            {
                var cluster = Cluster.Get(system);
                cluster.Join(cluster.SelfAddress);
            });
        }

        foreach (var registration in serviceProvider.GetServices<IActorRegistration>())
        {
            registration.Configure(builder, serviceProvider);
        }
    }
}
