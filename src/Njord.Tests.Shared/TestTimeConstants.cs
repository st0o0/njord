namespace Njord.Tests.Shared;

public static class TestTimeConstants
{
    public static readonly DateTimeOffset PipelineEpoch = new(2026, 7, 12, 6, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset NoonAnchor = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset ForecastEpoch = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset EnrichmentEpoch = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset BudgetEpoch = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
}
