using Akka.Hosting;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public abstract class PollingConnectionTestKit : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .AddTestPersistence()
            .AddTestTimefactor();
    }
}
