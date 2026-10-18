using Njord.Messages.Budget;

namespace Njord.Budget;

internal static class BudgetMessageMappings
{
    public static BudgetUsageResult GetSnapshot(this BudgetTrackerState state) =>
        new(state.MonthlyUsed, state.DailyUsed);
}
