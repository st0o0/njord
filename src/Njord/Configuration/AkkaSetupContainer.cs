using Akka.Cluster;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Persistence.Sql.Hosting;
using Akka.Remote.Hosting;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Servus.Akka.Startup;

namespace Njord.Configuration;

public sealed class AkkaSetupContainer : ActorSystemSetupContainer
{
    protected override string GetActorSystemName() => "njord";

    protected override void BuildSystem(AkkaConfigurationBuilder builder, IServiceProvider serviceProvider)
    {
        var persistence = serviceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        ConfigureLogging(builder, configuration);
        ConfigureInfrastructure(builder, persistence, persistence.DataPath, configuration);

        foreach (var registration in serviceProvider.GetServices<IActorRegistration>())
        {
            registration.Configure(builder, serviceProvider);
        }
    }

    internal static AkkaConfigurationBuilder ConfigureLogging(AkkaConfigurationBuilder builder, IConfiguration configuration) =>
        builder.ConfigureLoggers(loggers =>
        {
            loggers.LogLevel = AkkaLogLevel.From(configuration);
            loggers.ClearLoggers();
            loggers.AddLogger<ActorCategoryLogger>();
        });

    internal static void ConfigureInfrastructure(
        AkkaConfigurationBuilder builder, PersistenceOptions persistence, string persistencePath,
        IConfiguration configuration)
    {
        var selection = PersistenceSelection.From(persistence, persistencePath);
        var port = configuration.GetValue("Akka:RemotingPort", 2552);

        builder
            .WithSqlPersistence(selection.ConnectionString, selection.ProviderName, autoInitialize: true)
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
    }
}
