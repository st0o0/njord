using Njord.Messages.Budget;
using Njord.Persistence.Events.Budget;
using Njord.Tests.Shared;

namespace Njord.Budget.Tests;

public sealed class BudgetTrackerStateSpec
{
    private static readonly DateTimeOffset Now = TestTimeConstants.BudgetEpoch;
    private static readonly BudgetTrackerState Fresh = new(Now.Month, Now.DayOfYear, 0, 0);

    [Fact]
    public void Apply_adds_the_call_weight_to_monthly_and_daily_usage()
    {
        var state = Fresh
            .Apply(new ApiCallRecorded(3, Now), Now)
            .Apply(new ApiCallRecorded(2, Now), Now);

        Assert.Multiple(
            () => Assert.Equal(5, state.MonthlyUsed),
            () => Assert.Equal(5, state.DailyUsed));
    }

    [Fact]
    public void Apply_on_a_new_day_resets_daily_usage_and_keeps_monthly()
    {
        var tomorrow = Now.AddDays(1);

        var state = (Fresh with { MonthlyUsed = 10, DailyUsed = 4 }).Apply(new ApiCallRecorded(2, tomorrow), tomorrow);

        Assert.Equal(new BudgetTrackerState(tomorrow.Month, tomorrow.DayOfYear, 12, 2), state);
    }

    [Fact]
    public void Apply_in_a_new_month_resets_both_counters()
    {
        var nextMonth = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        var state = (Fresh with { MonthlyUsed = 10, DailyUsed = 4 }).Apply(new ApiCallRecorded(3, nextMonth), nextMonth);

        Assert.Equal(new BudgetTrackerState(8, nextMonth.DayOfYear, 3, 3), state);
    }

    [Fact]
    public void ProcessCommand_returns_the_event_stamped_with_the_current_time()
    {
        var (state, evt) = Fresh.ProcessCommand(new RecordApiCall(4), Now);

        Assert.Multiple(
            () => Assert.Equal(new ApiCallRecorded(4, Now), evt),
            () => Assert.Equal(4, state.MonthlyUsed));
    }

    [Fact]
    public void ApplyRecover_counts_events_of_the_current_month_and_only_todays_for_daily()
    {
        var state = Fresh
            .ApplyRecover(2, Now, Now)
            .ApplyRecover(3, Now.AddDays(-2), Now);

        Assert.Multiple(
            () => Assert.Equal(5, state.MonthlyUsed),
            () => Assert.Equal(2, state.DailyUsed));
    }

    [Fact]
    public void ApplyRecover_ignores_events_of_a_previous_month()
    {
        var state = Fresh.ApplyRecover(7, Now.AddMonths(-1), Now);

        Assert.Equal(Fresh, state);
    }
}
