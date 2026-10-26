using Njord.Persistence.Events.Budget;

namespace Njord.Persistence.Tests;

public sealed class BudgetTrackerRoundtripTests
{
    [Fact]
    public void Tracker_state_roundtrip()
    {
        var state = new PersistedBudgetTrackerState(7, 15, 12_345, 321);

        Assert.Equal(state, PersistenceRoundtripHelper.Roundtrip(state));
    }

    [Fact]
    public void Api_call_recorded_roundtrip()
    {
        var evt = new ApiCallRecorded(3, new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal(evt, PersistenceRoundtripHelper.Roundtrip(evt));
    }
}
