using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class HostProjectSpec
{
    [Fact]
    public void Host_assembly_is_reachable_from_the_host_test_project()
    {
        Assert.Equal("Njord", typeof(AkkaSetupContainer).Assembly.GetName().Name);
    }
}
