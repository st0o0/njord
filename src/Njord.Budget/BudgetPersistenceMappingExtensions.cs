using Njord.Persistence.Events.Budget;

namespace Njord.Budget;

internal static class BudgetPersistenceMappingExtensions
{
    public static PersistedBudgetTrackerState GetPersistenceState(this BudgetTrackerState state) =>
        new(state.CurrentMonth, state.CurrentDay, state.MonthlyUsed, state.DailyUsed);

    public static BudgetTrackerState FromPersistence(this PersistedBudgetTrackerState dto, DateTimeOffset now)
    {
        if (dto.Month != now.Month)
        {
            return new BudgetTrackerState(now.Month, now.DayOfYear, 0, 0);
        }

        if (dto.Day != now.DayOfYear)
        {
            return new BudgetTrackerState(now.Month, now.DayOfYear, dto.MonthlyUsed, 0);
        }

        return new BudgetTrackerState(now.Month, now.DayOfYear, dto.MonthlyUsed, dto.DailyUsed);
    }

    public static (int Weight, DateTimeOffset Utc) FromPersistedEvent(this ApiCallRecorded dto) =>
        (dto.Weight, dto.Utc);
}
