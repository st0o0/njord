using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Budget;
using Njord.Tests.Shared;

namespace Njord.Budget.Tests;

public sealed class BudgetTrackerStateSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.BudgetEpoch;

    private static BudgetTrackerState EmptyState() =>
        BudgetTrackerState.Empty with { CurrentMonth = T0.Month, CurrentDay = T0.DayOfYear };

    private static ApiCallRecorded Evt(int weight, DateTimeOffset utc) =>
        new(weight, utc);

    [Fact]
    public void Apply_increments_monthly_and_daily_usage()
    {
        var state = EmptyState().Apply(Evt(3, T0), T0);

        Assert.Equal(3, state.MonthlyUsed);
        Assert.Equal(3, state.DailyUsed);
    }

    [Fact]
    public void Apply_accumulates_multiple_calls()
    {
        var state = EmptyState()
            .Apply(Evt(3, T0), T0)
            .Apply(Evt(2, T0), T0)
            .Apply(Evt(1, T0), T0);

        Assert.Equal(6, state.MonthlyUsed);
        Assert.Equal(6, state.DailyUsed);
    }

    [Fact]
    public void Apply_resets_daily_on_new_day()
    {
        var state = EmptyState().Apply(Evt(5, T0), T0);
        var nextDay = T0.AddDays(1);

        var updated = state.Apply(Evt(2, nextDay), nextDay);

        Assert.Equal(7, updated.MonthlyUsed);
        Assert.Equal(2, updated.DailyUsed);
        Assert.Equal(nextDay.DayOfYear, updated.CurrentDay);
    }

    [Fact]
    public void Apply_resets_both_on_new_month()
    {
        var state = EmptyState().Apply(Evt(10, T0), T0);
        var nextMonth = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        var updated = state.Apply(Evt(3, nextMonth), nextMonth);

        Assert.Equal(3, updated.MonthlyUsed);
        Assert.Equal(3, updated.DailyUsed);
        Assert.Equal(8, updated.CurrentMonth);
    }

    [Fact]
    public void GetSnapshot_returns_current_usage()
    {
        var state = EmptyState().Apply(Evt(4, T0), T0).Apply(Evt(2, T0), T0);

        var snapshot = state.GetSnapshot();

        Assert.Equal(6, snapshot.MonthlyUsed);
        Assert.Equal(6, snapshot.DailyUsed);
    }

    [Fact]
    public void GetPersistenceState_returns_valid_dto()
    {
        var state = EmptyState().Apply(Evt(5, T0), T0);

        var dto = state.GetPersistenceState();

        Assert.Equal(T0.Month, dto.Month);
        Assert.Equal(T0.DayOfYear, dto.Day);
        Assert.Equal(5, dto.MonthlyUsed);
        Assert.Equal(5, dto.DailyUsed);
    }

    [Fact]
    public void FromPersistence_roundtrip_preserves_state()
    {
        var state = EmptyState()
            .Apply(Evt(3, T0), T0)
            .Apply(Evt(2, T0), T0);

        var dto = state.GetPersistenceState();
        var restored = BudgetTrackerState.FromPersistence(dto, T0);

        Assert.Equal(state.CurrentMonth, restored.CurrentMonth);
        Assert.Equal(state.CurrentDay, restored.CurrentDay);
        Assert.Equal(state.MonthlyUsed, restored.MonthlyUsed);
        Assert.Equal(state.DailyUsed, restored.DailyUsed);
    }

    [Fact]
    public void FromPersistence_resets_when_month_differs()
    {
        var state = EmptyState().Apply(Evt(10, T0), T0);
        var dto = state.GetPersistenceState();
        var nextMonth = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        var restored = BudgetTrackerState.FromPersistence(dto, nextMonth);

        Assert.Equal(0, restored.MonthlyUsed);
        Assert.Equal(0, restored.DailyUsed);
        Assert.Equal(8, restored.CurrentMonth);
    }

    [Fact]
    public void FromPersistence_resets_daily_when_day_differs()
    {
        var state = EmptyState().Apply(Evt(10, T0), T0);
        var dto = state.GetPersistenceState();
        var nextDay = T0.AddDays(1);

        var restored = BudgetTrackerState.FromPersistence(dto, nextDay);

        Assert.Equal(10, restored.MonthlyUsed);
        Assert.Equal(0, restored.DailyUsed);
    }

    [Fact]
    public void ApplyRecover_skips_events_from_old_months()
    {
        var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var state = new BudgetTrackerState(8, now.DayOfYear, 0, 0);
        var oldEvent = TestTimeConstants.BudgetEpoch;

        var result = state.ApplyRecover(5, oldEvent, now);

        Assert.Equal(0, result.MonthlyUsed);
        Assert.Equal(0, result.DailyUsed);
    }

    [Fact]
    public void ApplyRecover_adds_weight_for_current_month_events()
    {
        var state = new BudgetTrackerState(7, T0.DayOfYear, 0, 0);

        var result = state.ApplyRecover(5, T0, T0);

        Assert.Equal(5, result.MonthlyUsed);
        Assert.Equal(5, result.DailyUsed);
    }

    [Fact]
    public void ApplyRecover_adds_monthly_but_not_daily_for_different_day()
    {
        var now = T0;
        var eventUtc = T0.AddDays(-1);
        var state = new BudgetTrackerState(T0.Month, T0.DayOfYear, 0, 0);

        var result = state.ApplyRecover(3, eventUtc, now);

        Assert.Equal(3, result.MonthlyUsed);
        Assert.Equal(0, result.DailyUsed);
    }

    [Fact]
    public void Empty_state_has_zero_counters()
    {
        var state = BudgetTrackerState.Empty;

        Assert.Multiple(
            () => Assert.Equal(0, state.CurrentMonth),
            () => Assert.Equal(0, state.CurrentDay),
            () => Assert.Equal(0, state.MonthlyUsed),
            () => Assert.Equal(0, state.DailyUsed));
    }

    [Fact]
    public void ProcessCommand_returns_new_state_and_persistence_event()
    {
        var state = EmptyState();
        var cmd = new RecordApiCall(3);

        var (newState, evt) = state.ProcessCommand(cmd, T0);

        Assert.Multiple(
            () => Assert.Equal(3, newState.MonthlyUsed),
            () => Assert.Equal(3, newState.DailyUsed),
            () => Assert.Equal(3, evt.Weight),
            () => Assert.Equal(T0, evt.Utc));
    }

    [Fact]
    public void ProcessCommand_handles_month_boundary()
    {
        var state = EmptyState().Apply(Evt(10, T0), T0);
        var nextMonth = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        var (newState, evt) = state.ProcessCommand(new RecordApiCall(2), nextMonth);

        Assert.Multiple(
            () => Assert.Equal(2, newState.MonthlyUsed),
            () => Assert.Equal(2, newState.DailyUsed),
            () => Assert.Equal(8, newState.CurrentMonth),
            () => Assert.Equal(2, evt.Weight));
    }
}
