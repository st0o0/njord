using Akka.Hosting;

namespace Njord.Tests.Shared;

public static class TestTimefactorConfig
{
    public static AkkaConfigurationBuilder AddTestTimefactor(this AkkaConfigurationBuilder builder)
        => builder.AddHocon("akka.test.timefactor = 3", HoconAddMode.Prepend);
}
