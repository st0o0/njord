using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class BudgetTrackerSpec
{
    [Fact(Timeout = 5000)]
    public void RecordCall_increments_monthly_and_daily_usage()
    {
        var tracker = new BudgetTracker();

        tracker.RecordCall();
        tracker.RecordCall();

        var (monthly, daily) = tracker.GetUsage();
        Assert.Equal(2, monthly);
        Assert.Equal(2, daily);
    }

    [Fact(Timeout = 5000)]
    public void RecordCall_applies_weight()
    {
        var tracker = new BudgetTracker();

        tracker.RecordCall(weight: 4);

        var (monthly, daily) = tracker.GetUsage();
        Assert.Equal(4, monthly);
        Assert.Equal(4, daily);
    }

    [Fact(Timeout = 5000)]
    public void GetUsage_returns_zero_before_any_calls()
    {
        var tracker = new BudgetTracker();

        var (monthly, daily) = tracker.GetUsage();

        Assert.Equal(0, monthly);
        Assert.Equal(0, daily);
    }

    [Fact(Timeout = 5000)]
    public void Multiple_calls_accumulate()
    {
        var tracker = new BudgetTracker();

        tracker.RecordCall(weight: 3);
        tracker.RecordCall(weight: 2);
        tracker.RecordCall();

        var (monthly, daily) = tracker.GetUsage();
        Assert.Equal(6, monthly);
        Assert.Equal(6, daily);
    }
}
