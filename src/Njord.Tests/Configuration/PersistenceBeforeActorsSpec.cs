using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class PersistenceBeforeActorsSpec
{
    private static NjordOptions SqliteOptions() => new()
    {
        PersistencePath = Path.Combine(Path.GetTempPath(), $"njord-order-{Guid.NewGuid():N}.db"),
    };

    [Fact]
    public void Production_setup_configures_persistence_before_registering_actors()
    {
        var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "njord");

        var result = NjordActorSystemSetup.ConfigureSystem(builder, SqliteOptions());

        Assert.Same(builder, result);
        Assert.True(builder.Configuration.HasValue);
        Assert.True(builder.Configuration.Value.HasPath("akka.persistence.journal.plugin"));
    }

    [Fact]
    public void Registering_actors_without_persistence_fails_fast()
    {
        var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "njord");

        var ex = Assert.Throws<InvalidOperationException>(
            () => NjordActorSystemSetup.WithNjordActors(builder, mqttEnabled: false));

        Assert.Contains("Persistence must be configured", ex.Message);
    }
}
