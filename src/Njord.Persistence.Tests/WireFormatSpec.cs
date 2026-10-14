using Newtonsoft.Json;
using Njord.Persistence.Events.Budget;

namespace Njord.Persistence.Tests;

public sealed class WireFormatSpec
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
    };

    [Fact]
    public void PersistedBudgetTrackerState_wire_format()
    {
        var dto = new PersistedBudgetTrackerState(7, 193, 4200, 150);

        var json = JsonConvert.SerializeObject(dto, Settings);

        var expected = """
{
  "Month": 7,
  "Day": 193,
  "MonthlyUsed": 4200,
  "DailyUsed": 150
}
""".TrimEnd();
        Assert.Equal(expected.ReplaceLineEndings(), json.ReplaceLineEndings());
    }

    [Fact]
    public void ApiCallRecorded_wire_format()
    {
        var dto = new ApiCallRecorded(3, new DateTimeOffset(2024, 8, 15, 0, 0, 0, TimeSpan.Zero));

        var json = JsonConvert.SerializeObject(dto, Settings);

        var expected = """
{
  "Weight": 3,
  "Utc": "2024-08-15T00:00:00+00:00"
}
""".TrimEnd();
        Assert.Equal(expected.ReplaceLineEndings(), json.ReplaceLineEndings());
    }
}
