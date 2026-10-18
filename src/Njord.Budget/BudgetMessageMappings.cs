using Njord.Messages.Budget;

namespace Njord.Budget;

internal static class BudgetMessageMappings
{
    public static BudgetUsageResult ToBudgetUsage(this BudgetTrackerState state) =>
        new(state.MonthlyUsed, state.DailyUsed);
}
