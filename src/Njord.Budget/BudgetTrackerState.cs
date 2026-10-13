using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Budget;

namespace Njord.Budget;

public sealed record BudgetTrackerState(
    int CurrentMonth,
    int CurrentDay,
    long MonthlyUsed,
    long DailyUsed,
    int EventsSinceSnapshot)
{
    public static readonly BudgetTrackerState Empty = new(0, 0, 0, 0, 0);

    public static BudgetTrackerState FromPersistence(PersistedBudgetTrackerState dto, DateTimeOffset now)
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

public static class BudgetTrackerStateExtensions
{
    public static (BudgetTrackerState State, ApiCallRecorded Event) ProcessCommand(
        this BudgetTrackerState state, RecordApiCall cmd, DateTimeOffset now)
    {
        var evt = new ApiCallRecorded
        {
            Weight = cmd.Weight,
            UtcTicks = now.UtcTicks,
        };
        var newState = state.Apply(cmd.Weight, now);
        return (newState, evt);
    }

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
        {
            return state;
        }

        var newMonthly = state.MonthlyUsed + weight;
        var newDaily = eventUtc.DayOfYear == now.DayOfYear
            ? state.DailyUsed + weight
            : state.DailyUsed;

        return state with { MonthlyUsed = newMonthly, DailyUsed = newDaily };
    }

    public static BudgetUsageResult GetSnapshot(this BudgetTrackerState state) =>
        new(state.MonthlyUsed, state.DailyUsed);

    public static PersistedBudgetTrackerState GetPersistenceState(this BudgetTrackerState state) => new()
    {
        Month = state.CurrentMonth,
        Day = state.CurrentDay,
        MonthlyUsed = state.MonthlyUsed,
        DailyUsed = state.DailyUsed,
    };

    public static (int Weight, DateTimeOffset Utc) FromPersistedEvent(ApiCallRecorded dto) =>
        (dto.Weight, new DateTimeOffset(dto.UtcTicks, TimeSpan.Zero));
}
