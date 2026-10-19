using Njord.Persistence.Events.Budget;
using Njord.Tests.Shared;

namespace Njord.Budget.Tests;

public sealed class BudgetPersistenceMappingsSpec
{
    private static readonly DateTimeOffset Now = TestTimeConstants.BudgetEpoch;

    [Fact]
    public void A_snapshot_of_today_restores_both_counters()
    {
        var restored = new PersistedBudgetTrackerState(Now.Month, Now.DayOfYear, 100, 7).FromPersistence(Now);

        Assert.Equal(new BudgetTrackerState(Now.Month, Now.DayOfYear, 100, 7), restored);
    }

    [Fact]
    public void A_snapshot_of_an_earlier_day_restores_only_the_monthly_counter()
    {
        var restored = new PersistedBudgetTrackerState(Now.Month, Now.DayOfYear - 1, 100, 7).FromPersistence(Now);

        Assert.Equal(new BudgetTrackerState(Now.Month, Now.DayOfYear, 100, 0), restored);
    }

    [Fact]
    public void A_snapshot_of_an_earlier_month_restores_nothing()
    {
        var restored = new PersistedBudgetTrackerState(Now.Month - 1, 150, 100, 7).FromPersistence(Now);

        Assert.Equal(new BudgetTrackerState(Now.Month, Now.DayOfYear, 0, 0), restored);
    }
}
