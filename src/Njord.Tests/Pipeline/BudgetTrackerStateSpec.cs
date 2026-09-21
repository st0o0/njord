using Njord.Pipeline;

namespace Njord.Tests.Pipeline;

public sealed class BudgetTrackerStateSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private static BudgetTrackerState EmptyState() =>
        new(T0.Month, T0.DayOfYear, 0, 0, 0);

    [Fact(Timeout = 5000)]
    public void Apply_increments_monthly_and_daily_usage()
    {
        var state = EmptyState().Apply(3, T0);

        Assert.Equal(3, state.MonthlyUsed);
        Assert.Equal(3, state.DailyUsed);
        Assert.Equal(1, state.EventsSinceSnapshot);
    }

    [Fact(Timeout = 5000)]
    public void Apply_accumulates_multiple_calls()
    {
        var state = EmptyState()
            .Apply(3, T0)
            .Apply(2, T0)
            .Apply(1, T0);

        Assert.Equal(6, state.MonthlyUsed);
        Assert.Equal(6, state.DailyUsed);
        Assert.Equal(3, state.EventsSinceSnapshot);
    }

    [Fact(Timeout = 5000)]
    public void Apply_resets_daily_on_new_day()
    {
        var state = EmptyState().Apply(5, T0);
        var nextDay = T0.AddDays(1);

        var updated = state.Apply(2, nextDay);

        Assert.Equal(7, updated.MonthlyUsed);
        Assert.Equal(2, updated.DailyUsed);
        Assert.Equal(nextDay.DayOfYear, updated.CurrentDay);
    }

    [Fact(Timeout = 5000)]
    public void Apply_resets_both_on_new_month()
    {
        var state = EmptyState().Apply(10, T0);
        var nextMonth = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        var updated = state.Apply(3, nextMonth);

        Assert.Equal(3, updated.MonthlyUsed);
        Assert.Equal(3, updated.DailyUsed);
        Assert.Equal(8, updated.CurrentMonth);
    }

    [Fact(Timeout = 5000)]
    public void GetSnapshot_returns_current_usage()
    {
        var state = EmptyState().Apply(4, T0).Apply(2, T0);

        var snapshot = state.GetSnapshot();

        Assert.Equal(6, snapshot.MonthlyUsed);
        Assert.Equal(6, snapshot.DailyUsed);
    }

    [Fact(Timeout = 5000)]
    public void GetPersistenceState_returns_valid_dto()
    {
        var state = EmptyState().Apply(5, T0);

        var dto = state.GetPersistenceState();

        Assert.Equal(T0.Month, dto.Month);
        Assert.Equal(T0.DayOfYear, dto.Day);
        Assert.Equal(5, dto.MonthlyUsed);
        Assert.Equal(5, dto.DailyUsed);
    }

    [Fact(Timeout = 5000)]
    public void FromPersistence_roundtrip_preserves_state()
    {
        var state = EmptyState()
            .Apply(3, T0)
            .Apply(2, T0);

        var dto = state.GetPersistenceState();
        var restored = BudgetTrackerStateExtensions.FromPersistence(dto, T0);

        Assert.Equal(state.CurrentMonth, restored.CurrentMonth);
        Assert.Equal(state.CurrentDay, restored.CurrentDay);
        Assert.Equal(state.MonthlyUsed, restored.MonthlyUsed);
        Assert.Equal(state.DailyUsed, restored.DailyUsed);
    }

    [Fact(Timeout = 5000)]
    public void FromPersistence_resets_when_month_differs()
    {
        var state = EmptyState().Apply(10, T0);
        var dto = state.GetPersistenceState();
        var nextMonth = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        var restored = BudgetTrackerStateExtensions.FromPersistence(dto, nextMonth);

        Assert.Equal(0, restored.MonthlyUsed);
        Assert.Equal(0, restored.DailyUsed);
        Assert.Equal(8, restored.CurrentMonth);
    }

    [Fact(Timeout = 5000)]
    public void FromPersistence_resets_daily_when_day_differs()
    {
        var state = EmptyState().Apply(10, T0);
        var dto = state.GetPersistenceState();
        var nextDay = T0.AddDays(1);

        var restored = BudgetTrackerStateExtensions.FromPersistence(dto, nextDay);

        Assert.Equal(10, restored.MonthlyUsed);
        Assert.Equal(0, restored.DailyUsed);
    }

    [Fact(Timeout = 5000)]
    public void ApplyRecover_skips_events_from_old_months()
    {
        var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var state = new BudgetTrackerState(8, now.DayOfYear, 0, 0, 0);
        var oldEvent = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        var result = state.ApplyRecover(5, oldEvent, now);

        Assert.Equal(0, result.MonthlyUsed);
        Assert.Equal(0, result.DailyUsed);
    }

    [Fact(Timeout = 5000)]
    public void ApplyRecover_adds_weight_for_current_month_events()
    {
        var state = new BudgetTrackerState(7, T0.DayOfYear, 0, 0, 0);

        var result = state.ApplyRecover(5, T0, T0);

        Assert.Equal(5, result.MonthlyUsed);
        Assert.Equal(5, result.DailyUsed);
    }

    [Fact(Timeout = 5000)]
    public void ApplyRecover_adds_monthly_but_not_daily_for_different_day()
    {
        var now = T0;
        var eventUtc = T0.AddDays(-1);
        var state = new BudgetTrackerState(T0.Month, T0.DayOfYear, 0, 0, 0);

        var result = state.ApplyRecover(3, eventUtc, now);

        Assert.Equal(3, result.MonthlyUsed);
        Assert.Equal(0, result.DailyUsed);
    }

    [Fact(Timeout = 5000)]
    public void Empty_state_has_zero_counters()
    {
        var state = EmptyState();

        Assert.Equal(0, state.MonthlyUsed);
        Assert.Equal(0, state.DailyUsed);
        Assert.Equal(0, state.EventsSinceSnapshot);
    }
}
