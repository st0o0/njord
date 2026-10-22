namespace Njord.Core.Configuration.Health;

public sealed class HealthOptions : IConfigSection
{
    private const string SectionName = "Njord:Health";
    static string IConfigSection.SectionName => SectionName;

    public double PollOverdueFactor { get; set; } = 2.0;
    public double PollStalledFactor { get; set; } = 3.0;
    public double BudgetDegradedBelowPercent { get; set; } = 5.0;
}
