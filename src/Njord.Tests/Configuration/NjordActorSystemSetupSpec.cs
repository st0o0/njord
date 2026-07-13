using Njord.Configuration;
using Servus.Akka.Startup;

namespace Njord.Tests.Configuration;

public sealed class NjordActorSystemSetupSpec
{
    [Fact(Timeout = 5000)]
    public void Extends_ActorSystemSetupContainer()
    {
        var setup = new NjordActorSystemSetup();

        Assert.IsAssignableFrom<ActorSystemSetupContainer>(setup);
    }

    [Fact(Timeout = 5000)]
    public void Actor_system_name_is_njord()
    {
        var setup = new NjordActorSystemSetup();
        var name = typeof(ActorSystemSetupContainer)
            .GetMethod("GetActorSystemName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(setup, null) as string;

        Assert.Equal("njord", name);
    }
}
