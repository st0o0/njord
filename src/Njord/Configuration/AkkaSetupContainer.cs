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
        var njordOptions = serviceProvider.GetRequiredService<IOptions<NjordOptions>>().Value;
        ConfigureInfrastructure(builder, njordOptions);

        foreach (var registration in serviceProvider.GetServices<IActorRegistration>())
        {
            registration.Configure(builder, serviceProvider);
        }
    }

    internal static void ConfigureInfrastructure(AkkaConfigurationBuilder builder, NjordOptions njordOptions)
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
            .WithRemoting(new RemoteOptions { HostName = "localhost", Port = 2552 })
            .WithClustering(new ClusterOptions
            {
                SeedNodes = ["akka.tcp://njord@localhost:2552"]
            });
    }
}
