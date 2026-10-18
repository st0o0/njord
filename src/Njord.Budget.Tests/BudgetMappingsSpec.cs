using Njord.Persistence.Events.Budget;
using Njord.Tests.Shared;

namespace Njord.Budget.Tests;

public sealed class BudgetMappingsSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.BudgetEpoch;

    private static BudgetTrackerState StateWith(int weight) =>
        (BudgetTrackerState.Empty with { CurrentMonth = T0.Month, CurrentDay = T0.DayOfYear })
        .Apply(new ApiCallRecorded(weight, T0), T0);

    [Fact]
    public void GetSnapshot_returns_current_usage()
    {
        var state = StateWith(4).Apply(new ApiCallRecorded(2, T0), T0);

        var snapshot = state.GetSnapshot();

        Assert.Multiple(
            () => Assert.Equal(6, snapshot.MonthlyUsed),
            () => Assert.Equal(6, snapshot.DailyUsed));
    }

    [Fact]
    public void GetPersistenceState_returns_valid_dto()
    {
        var dto = StateWith(5).GetPersistenceState();

        Assert.Equal(new PersistedBudgetTrackerState(T0.Month, T0.DayOfYear, 5, 5), dto);
    }

    [Fact]
    public void FromPersistence_roundtrip_preserves_state()
    {
        var state = StateWith(3).Apply(new ApiCallRecorded(2, T0), T0);

        var restored = state.GetPersistenceState().FromPersistence(T0);

        Assert.Equal(state, restored);
    }

    [Fact]
    public void FromPersistence_resets_when_month_differs()
    {
        var dto = StateWith(10).GetPersistenceState();
        var nextMonth = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        var restored = dto.FromPersistence(nextMonth);

        Assert.Equal(new BudgetTrackerState(8, nextMonth.DayOfYear, 0, 0), restored);
    }

    [Fact]
    public void FromPersistence_resets_daily_when_day_differs()
    {
        var dto = StateWith(10).GetPersistenceState();
        var nextDay = T0.AddDays(1);

        var restored = dto.FromPersistence(nextDay);

        Assert.Equal(new BudgetTrackerState(T0.Month, nextDay.DayOfYear, 10, 0), restored);
    }

    [Fact]
    public void FromPersistedEvent_returns_weight_and_timestamp()
    {
        var (weight, utc) = new ApiCallRecorded(7, T0).FromPersistedEvent();

        Assert.Multiple(
            () => Assert.Equal(7, weight),
            () => Assert.Equal(T0, utc));
    }
}
