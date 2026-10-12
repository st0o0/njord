using Newtonsoft.Json;

namespace Njord.Persistence.Events.Budget;

public sealed record PersistedBudgetTrackerState
{
    [JsonProperty("month")] public int Month { get; init; }
    [JsonProperty("day")] public int Day { get; init; }
    [JsonProperty("monthly")] public long MonthlyUsed { get; init; }
    [JsonProperty("daily")] public long DailyUsed { get; init; }
}
