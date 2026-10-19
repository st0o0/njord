using Akka.Hosting;
using Akka.Streams;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public abstract class SinkRefTestKit : Akka.Hosting.TestKit.TestKit
{
    protected IMaterializer Mat => Sys.Materializer();

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .AddTestPersistence()
            .AddTestTimefactor();
    }
}
