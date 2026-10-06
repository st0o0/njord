namespace Njord.Persistence.Tests;

public sealed class BudgetTrackerDtoRoundtripSpec
{
    [Fact]
    public void BudgetTrackerSnapshotDto_roundtrip()
    {
        var dto = new BudgetTrackerSnapshotDto
        {
            Version = 1,
            Month = 7,
            Day = 12,
            MonthlyUsed = 4200,
            DailyUsed = 150,
        };

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(dto.Version, result.Version);
        Assert.Equal(dto.Month, result.Month);
        Assert.Equal(dto.Day, result.Day);
        Assert.Equal(dto.MonthlyUsed, result.MonthlyUsed);
        Assert.Equal(dto.DailyUsed, result.DailyUsed);
    }

    [Fact]
    public void ApiCallRecordedDto_roundtrip()
    {
        var dto = new ApiCallRecordedDto
        {
            Version = 1,
            Weight = 3,
            UtcTicks = 638_600_000_000_000_000L,
        };

        var result = PersistenceRoundtripHelper.Roundtrip(dto);

        Assert.Equal(dto.Version, result.Version);
        Assert.Equal(dto.Weight, result.Weight);
        Assert.Equal(dto.UtcTicks, result.UtcTicks);
    }
}
