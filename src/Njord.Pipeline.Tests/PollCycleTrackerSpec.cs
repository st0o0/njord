namespace Njord.Pipeline.Tests;

public sealed class PollCycleTrackerSpec
{
    [Fact]
    public void A_summary_carries_the_location_and_the_cycle_duration()
    {
        var summary = new PollCycleSummary("lucerne", TimeSpan.FromSeconds(8));

        Assert.Multiple(
            () => Assert.Equal("lucerne", summary.Location),
            () => Assert.Equal(TimeSpan.FromSeconds(8), summary.Duration));
    }
}
