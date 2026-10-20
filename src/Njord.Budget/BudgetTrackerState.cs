using Njord.Messages.Budget;
using Njord.Persistence.Events.Budget;

namespace Njord.Budget;

public sealed record BudgetTrackerState(
    int CurrentMonth,
    int CurrentDay,
    long MonthlyUsed,
    long DailyUsed)
{
    public static readonly BudgetTrackerState Empty = new(0, 0, 0, 0);
}
