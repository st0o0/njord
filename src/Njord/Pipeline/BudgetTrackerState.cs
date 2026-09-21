using Njord.Persistence;

namespace Njord.Pipeline;

public sealed record BudgetTrackerState(
    int CurrentMonth,
    int CurrentDay,
    long MonthlyUsed,
    long DailyUsed,
    int EventsSinceSnapshot)
{
    public static BudgetTrackerState Empty(TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return new BudgetTrackerState(now.Month, now.DayOfYear, 0, 0, 0);
    }
}

public static class BudgetTrackerStateExtensions
{
    public static BudgetTrackerState Apply(this BudgetTrackerState state, int weight, DateTimeOffset now)
    {
        if (now.Month != state.CurrentMonth)
        {
            return state with
            {
                CurrentMonth = now.Month,
                CurrentDay = now.DayOfYear,
                MonthlyUsed = weight,
                DailyUsed = weight,
                EventsSinceSnapshot = state.EventsSinceSnapshot + 1,
            };
        }

        if (now.DayOfYear != state.CurrentDay)
        {
            return state with
            {
                CurrentDay = now.DayOfYear,
                MonthlyUsed = state.MonthlyUsed + weight,
                DailyUsed = weight,
                EventsSinceSnapshot = state.EventsSinceSnapshot + 1,
            };
        }

        return state with
        {
            MonthlyUsed = state.MonthlyUsed + weight,
            DailyUsed = state.DailyUsed + weight,
            EventsSinceSnapshot = state.EventsSinceSnapshot + 1,
        };
    }

    public static BudgetTrackerState ApplyRecover(
        this BudgetTrackerState state, int weight, DateTimeOffset eventUtc, DateTimeOffset now)
    {
        if (eventUtc.Month != now.Month || eventUtc.Year != now.Year)
            return state;

        var newMonthly = state.MonthlyUsed + weight;
        var newDaily = eventUtc.DayOfYear == now.DayOfYear
            ? state.DailyUsed + weight
            : state.DailyUsed;

        return state with { MonthlyUsed = newMonthly, DailyUsed = newDaily };
    }

    public static BudgetUsageResult GetSnapshot(this BudgetTrackerState state) =>
        new(state.MonthlyUsed, state.DailyUsed);

    public static BudgetTrackerSnapshotDto GetPersistenceState(this BudgetTrackerState state) =>
        BudgetTrackerDtoMapping.ToSnapshot(state.CurrentMonth, state.CurrentDay, state.MonthlyUsed, state.DailyUsed);

    public static BudgetTrackerState FromPersistence(BudgetTrackerSnapshotDto dto, DateTimeOffset now)
    {
        if (dto.Month != now.Month)
        {
            return new BudgetTrackerState(now.Month, now.DayOfYear, 0, 0, 0);
        }

        if (dto.Day != now.DayOfYear)
        {
            return new BudgetTrackerState(now.Month, now.DayOfYear, dto.MonthlyUsed, 0, 0);
        }

        return new BudgetTrackerState(now.Month, now.DayOfYear, dto.MonthlyUsed, dto.DailyUsed, 0);
    }
}
