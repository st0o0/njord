using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Hosting;

namespace Njord.Tests.Shared;

public abstract class NjordActorSpec : Akka.Hosting.TestKit.TestKit
{
    protected virtual bool UseTestPersistence => true;

    protected IActorRef GetActor<TKey>() => ActorRegistry.Get<TKey>();

    protected sealed override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        if (UseTestPersistence)
        {
            builder.AddTestPersistence();
        }

        ConfigureActors(builder, provider);
        builder.AddTestTimefactor();
    }

    protected virtual void ConfigureActors(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
    }
}

public static class TestAkkaConfig
{
    public static AkkaConfigurationBuilder AddTestPersistence(this AkkaConfigurationBuilder builder)
        => builder.WithInMemoryJournal().WithInMemorySnapshotStore();

    public static AkkaConfigurationBuilder AddTestTimefactor(this AkkaConfigurationBuilder builder)
        => builder.AddHocon($"akka.test.timefactor = {TestTimeouts.TimeFactor}", HoconAddMode.Prepend);
}
