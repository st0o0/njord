using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Budget;

namespace Njord.Budget;

public sealed record BudgetTrackerState(
    int CurrentMonth,
    int CurrentDay,
    long MonthlyUsed,
    long DailyUsed)
{
    public static readonly BudgetTrackerState Empty = new(0, 0, 0, 0);

    public static BudgetTrackerState FromPersistence(PersistedBudgetTrackerState dto, DateTimeOffset now)
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
}

public static class BudgetTrackerStateExtensions
{
    public static (BudgetTrackerState State, ApiCallRecorded Event) ProcessCommand(
        this BudgetTrackerState state, RecordApiCall cmd, DateTimeOffset now)
    {
        var evt = new ApiCallRecorded(cmd.Weight, now);
        var newState = state.Apply(evt, now);
        return (newState, evt);
    }

    public static BudgetTrackerState Apply(this BudgetTrackerState state, ApiCallRecorded evt, DateTimeOffset now)
    {
        var weight = evt.Weight;

        if (now.Month != state.CurrentMonth)
        {
            return state with
            {
                CurrentMonth = now.Month,
                CurrentDay = now.DayOfYear,
                MonthlyUsed = weight,
                DailyUsed = weight,
            };
        }

        if (now.DayOfYear != state.CurrentDay)
        {
            return state with
            {
                CurrentDay = now.DayOfYear,
                MonthlyUsed = state.MonthlyUsed + weight,
                DailyUsed = weight,
            };
        }

        return state with
        {
            MonthlyUsed = state.MonthlyUsed + weight,
            DailyUsed = state.DailyUsed + weight,
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

    public static PersistedBudgetTrackerState GetPersistenceState(this BudgetTrackerState state) =>
        new(state.CurrentMonth, state.CurrentDay, state.MonthlyUsed, state.DailyUsed);

    public static (int Weight, DateTimeOffset Utc) FromPersistedEvent(ApiCallRecorded dto) =>
        (dto.Weight, dto.Utc);
}
