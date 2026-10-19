using Akka.Hosting;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public abstract class PipelineConnectionTestKit : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .AddTestPersistence()
            .AddTestTimefactor();
    }
}
