using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Persistence.Sql.Hosting;
using Akka.Remote.Hosting;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration.Persistence;
using Servus.Akka.Startup;

namespace Njord.Configuration;

public sealed class AkkaSetupContainer : ActorSystemSetupContainer
{
    protected override string GetActorSystemName() => "njord";

    protected override void BuildSystem(AkkaConfigurationBuilder builder, IServiceProvider serviceProvider)
    {
        // The actor system configuration is built once at startup and needs plain values, so the validated
        // options are read here.
        var persistence = serviceProvider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
        var storage = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
        ConfigureLogging(builder, serviceProvider.GetRequiredService<IConfiguration>());
        ConfigureInfrastructure(builder, persistence, storage.PersistencePath);

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
        AkkaConfigurationBuilder builder, PersistenceOptions persistence, string persistencePath)
    {
        var selection = PersistenceSelection.From(persistence, persistencePath);

        builder
            .WithSqlPersistence(selection.ConnectionString, selection.ProviderName, autoInitialize: true)
            .WithRemoting(new RemoteOptions { HostName = "localhost", Port = 2552 })
            .WithClustering(new ClusterOptions
            {
                SeedNodes = ["akka.tcp://njord@localhost:2552"]
            });
    }
}
