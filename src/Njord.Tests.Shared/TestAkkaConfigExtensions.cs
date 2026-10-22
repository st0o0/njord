using Akka.Hosting;
using Akka.Persistence.Hosting;

namespace Njord.Tests.Shared;

public static class TestAkkaConfigExtensions
{
    public static AkkaConfigurationBuilder AddTestPersistence(this AkkaConfigurationBuilder builder)
        => builder.WithInMemoryJournal().WithInMemorySnapshotStore();

    public static AkkaConfigurationBuilder AddTestTimefactor(this AkkaConfigurationBuilder builder)
        => builder.AddHocon($"akka.test.timefactor = {TestTimeouts.TimeFactor}", HoconAddMode.Prepend);
}
