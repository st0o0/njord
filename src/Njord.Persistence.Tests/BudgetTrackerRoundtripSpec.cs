using Njord.Persistence.Events.Budget;

namespace Njord.Persistence.Tests;

public sealed class BudgetTrackerRoundtripSpec
{
    [Fact]
    public void Should_roundtrip_the_tracker_state()
    {
        var state = new PersistedBudgetTrackerState(7, 15, 12_345, 321);

        Assert.Equal(state, PersistenceRoundtripHelper.Roundtrip(state));
    }

    [Fact]
    public void Should_roundtrip_a_recorded_api_call()
    {
        var evt = new ApiCallRecorded(3, new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal(evt, PersistenceRoundtripHelper.Roundtrip(evt));
    }
}
