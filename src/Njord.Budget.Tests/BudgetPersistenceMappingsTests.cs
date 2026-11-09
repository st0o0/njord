using Njord.Persistence.Events.Budget;
using Njord.Tests.Shared;

namespace Njord.Budget.Tests;

public sealed class BudgetPersistenceMappingsTests
{
    private static readonly DateTimeOffset _now = TestTimeConstants.BudgetEpoch;

    [Fact]
    public void A_snapshot_of_today_restores_both_counters()
    {
        var restored = new PersistedBudgetTrackerState(_now.Month, _now.DayOfYear, 100, 7).FromPersistence(_now);

        Assert.Equal(new BudgetTrackerState(_now.Month, _now.DayOfYear, 100, 7), restored);
    }

    [Fact]
    public void A_snapshot_of_an_earlier_day_restores_only_the_monthly_counter()
    {
        var restored = new PersistedBudgetTrackerState(_now.Month, _now.DayOfYear - 1, 100, 7).FromPersistence(_now);

        Assert.Equal(new BudgetTrackerState(_now.Month, _now.DayOfYear, 100, 0), restored);
    }

    [Fact]
    public void A_snapshot_of_an_earlier_month_restores_nothing()
    {
        var restored = new PersistedBudgetTrackerState(_now.Month - 1, 150, 100, 7).FromPersistence(_now);

        Assert.Equal(new BudgetTrackerState(_now.Month, _now.DayOfYear, 0, 0), restored);
    }
}
