using Akka.Hosting;

namespace Njord.Tests.Shared;

public static class TestRetryBackoffConfig
{
    public static AkkaConfigurationBuilder AddFastRetryBackoff(this AkkaConfigurationBuilder builder)
        => builder.AddHocon("njord.retry-backoff.override = 10ms", HoconAddMode.Prepend);
}
