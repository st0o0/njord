namespace Njord.Persistence;

public static class BudgetTrackerDtoMapping
{
    public static ApiCallRecordedDto ToDto(int weight, DateTimeOffset utc) => new()
    {
        Weight = weight,
        UtcTicks = utc.UtcTicks,
    };

    public static (int Weight, DateTimeOffset Utc) ToDomain(ApiCallRecordedDto dto) =>
        (dto.Weight, new DateTimeOffset(dto.UtcTicks, TimeSpan.Zero));

    public static BudgetTrackerSnapshotDto ToSnapshot(int month, int day, long monthlyUsed, long dailyUsed) => new()
    {
        Month = month,
        Day = day,
        MonthlyUsed = monthlyUsed,
        DailyUsed = dailyUsed,
    };
}
