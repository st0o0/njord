namespace Njord.Persistence.Events.Budget;

public sealed record PersistedBudgetTrackerState(
    int Month, int Day, long MonthlyUsed, long DailyUsed);
