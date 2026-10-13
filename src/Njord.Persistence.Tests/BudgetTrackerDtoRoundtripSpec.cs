using Njord.Persistence.Events.Budget;

namespace Njord.Persistence.Tests;

public sealed class BudgetTrackerDtoRoundtripSpec
{
    [Fact]
    public void PersistedBudgetTrackerState_roundtrip()
    {
        var dto = new PersistedBudgetTrackerState(7, 12, 4200, 150);

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(dto.Month, result.Month);
        Assert.Equal(dto.Day, result.Day);
        Assert.Equal(dto.MonthlyUsed, result.MonthlyUsed);
        Assert.Equal(dto.DailyUsed, result.DailyUsed);
    }

    [Fact]
    public void ApiCallRecorded_roundtrip()
    {
        var dto = new ApiCallRecorded(3, new DateTimeOffset(2024, 8, 15, 0, 0, 0, TimeSpan.Zero));

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(dto.Weight, result.Weight);
        Assert.Equal(dto.Utc, result.Utc);
    }
}
