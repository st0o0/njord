using Newtonsoft.Json;
using Njord.Persistence.Events.Budget;
using PersistedDataChanged = Njord.Persistence.Events.Pipeline.DataChanged;

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
        var dto = new PersistedBudgetTrackerState
        {
            Month = 7,
            Day = 193,
            MonthlyUsed = 4200,
            DailyUsed = 150,
        };

        var json = JsonConvert.SerializeObject(dto, Settings);

        var expected = """
{
  "month": 7,
  "day": 193,
  "monthly": 4200,
  "daily": 150
}
""".TrimEnd();
        Assert.Equal(expected.ReplaceLineEndings(), json.ReplaceLineEndings());
    }

    [Fact]
    public void ApiCallRecorded_wire_format()
    {
        var dto = new ApiCallRecorded
        {
            Weight = 3,
            UtcTicks = 638_600_000_000_000_000L,
        };

        var json = JsonConvert.SerializeObject(dto, Settings);

        var expected = """
{
  "w": 3,
  "utc": 638600000000000000
}
""".TrimEnd();
        Assert.Equal(expected.ReplaceLineEndings(), json.ReplaceLineEndings());
    }

    [Fact]
    public void DataChanged_wire_format()
    {
        var dto = new PersistedDataChanged
        {
            Location = "lucerne",
            ModelId = "icon_d2",
            Hash = 42,
            UtcTicks = 638_600_000_000_000_000L,
        };

        var json = JsonConvert.SerializeObject(dto, Settings);

        var expected = """
{
  "loc": "lucerne",
  "model": "icon_d2",
  "hash": 42,
  "utc": 638600000000000000
}
""".TrimEnd();
        Assert.Equal(expected.ReplaceLineEndings(), json.ReplaceLineEndings());
    }
}
