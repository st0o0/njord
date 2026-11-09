using Njord.Messages.Budget;
using Njord.Persistence.Events.Budget;
using Njord.Tests.Shared;

namespace Njord.Budget.Tests;

public sealed class BudgetTrackerStateTests
{
    private static readonly DateTimeOffset _now = TestTimeConstants.BudgetEpoch;
    private static readonly BudgetTrackerState _fresh = new(_now.Month, _now.DayOfYear, 0, 0);

    [Fact]
    public void Apply_adds_the_call_weight_to_monthly_and_daily_usage()
    {
        var state = _fresh
            .Apply(new ApiCallRecorded(3, _now), _now)
            .Apply(new ApiCallRecorded(2, _now), _now);

        Assert.Multiple(
            () => Assert.Equal(5, state.MonthlyUsed),
            () => Assert.Equal(5, state.DailyUsed));
    }

    [Fact]
    public void Apply_on_a_new_day_resets_daily_usage_and_keeps_monthly()
    {
        var tomorrow = _now.AddDays(1);

        var state = (_fresh with { MonthlyUsed = 10, DailyUsed = 4 }).Apply(new ApiCallRecorded(2, tomorrow), tomorrow);

        Assert.Equal(new BudgetTrackerState(tomorrow.Month, tomorrow.DayOfYear, 12, 2), state);
    }

    [Fact]
    public void Apply_in_a_new_month_resets_both_counters()
    {
        var nextMonth = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        var state = (_fresh with { MonthlyUsed = 10, DailyUsed = 4 }).Apply(new ApiCallRecorded(3, nextMonth), nextMonth);

        Assert.Equal(new BudgetTrackerState(8, nextMonth.DayOfYear, 3, 3), state);
    }

    [Fact]
    public void ProcessCommand_returns_the_event_stamped_with_the_current_time()
    {
        var (state, evt) = _fresh.ProcessCommand(new RecordApiCall(4), _now);

        Assert.Multiple(
            () => Assert.Equal(new ApiCallRecorded(4, _now), evt),
            () => Assert.Equal(4, state.MonthlyUsed));
    }

    [Fact]
    public void ApplyRecover_counts_events_of_the_current_month_and_only_todays_for_daily()
    {
        var state = _fresh
            .ApplyRecover(2, _now, _now)
            .ApplyRecover(3, _now.AddDays(-2), _now);

        Assert.Multiple(
            () => Assert.Equal(5, state.MonthlyUsed),
            () => Assert.Equal(2, state.DailyUsed));
    }

    [Fact]
    public void ApplyRecover_ignores_events_of_a_previous_month()
    {
        var state = _fresh.ApplyRecover(7, _now.AddMonths(-1), _now);

        Assert.Equal(_fresh, state);
    }
}
