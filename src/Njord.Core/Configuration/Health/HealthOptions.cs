namespace Njord.Core.Configuration.Health;

public sealed class HealthOptions
{
    public const string SectionName = "Njord:Health";

    public double PollOverdueFactor { get; set; } = 2.0;
    public double PollStalledFactor { get; set; } = 3.0;
    public double BudgetDegradedBelowPercent { get; set; } = 5.0;
}
