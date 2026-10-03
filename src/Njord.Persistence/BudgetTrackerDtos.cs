using Newtonsoft.Json;

namespace Njord.Persistence;

public sealed class ApiCallRecordedDto
{
    [JsonProperty("v")] public int Version { get; set; } = 1;
    [JsonProperty("w")] public int Weight { get; set; }
    [JsonProperty("utc")] public long UtcTicks { get; set; }
}

public sealed class BudgetTrackerSnapshotDto
{
    [JsonProperty("v")] public int Version { get; set; } = 1;
    [JsonProperty("month")] public int Month { get; set; }
    [JsonProperty("day")] public int Day { get; set; }
    [JsonProperty("monthly")] public long MonthlyUsed { get; set; }
    [JsonProperty("daily")] public long DailyUsed { get; set; }
}
