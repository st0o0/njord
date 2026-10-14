using Akka.Hosting;
using Akka.Hosting.TestKit;
using Njord.Tests.Shared;

namespace Njord.Core.Tests;

public sealed class TestTimeoutsSpec : TestKit
{
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HostedTimeout = TimeSpan.FromMilliseconds(TestTimeouts.Hosted);
    private static readonly TimeSpan TestKitDefaultWait = TimeSpan.FromSeconds(3);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
        => builder.AddTestTimefactor();

    [Fact]
    public void Configured_timefactor_matches_the_shared_constant()
    {
        Assert.Equal(TestTimeouts.TimeFactor, TestKitSettings.TestTimeFactor);
    }

    [Fact]
    public void Dilated_await_assert_max_stays_below_the_hosted_timeout_with_margin()
    {
        var dilated = Dilated(TestTimeouts.AwaitAssertMax);

        Assert.True(dilated + Margin <= HostedTimeout,
            $"AwaitAssertMax {TestTimeouts.AwaitAssertMax} dilates to {dilated}; plus {Margin} margin it exceeds Hosted {HostedTimeout}");
    }

    [Fact]
    public void Real_time_wait_max_equals_the_dilated_await_assert_max()
    {
        Assert.Equal(Dilated(TestTimeouts.AwaitAssertMax), TestTimeouts.RealTimeWaitMax);
    }

    [Fact]
    public void Real_time_wait_max_stays_below_the_hosted_timeout_with_margin()
    {
        Assert.True(TestTimeouts.RealTimeWaitMax + Margin <= HostedTimeout,
            $"RealTimeWaitMax {TestTimeouts.RealTimeWaitMax} plus {Margin} margin exceeds Hosted {HostedTimeout}");
    }

    [Fact]
    public void Dilated_testkit_default_stays_below_the_hosted_timeout_with_margin()
    {
        var dilated = Dilated(TestKitDefaultWait);

        Assert.Multiple(
            () => Assert.Equal(TimeSpan.FromSeconds(9), dilated),
            () => Assert.True(dilated + Margin <= HostedTimeout,
                $"TestKit default wait dilates to {dilated}; plus {Margin} margin it exceeds Hosted {HostedTimeout}"));
    }
}
