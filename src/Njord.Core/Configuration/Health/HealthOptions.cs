namespace Njord.Core.Configuration.Health;

public sealed class HealthOptions
{
    public const string SectionName = "Njord:Health";

    public double PollOverdueFactor { get; set; }
    public double PollStalledFactor { get; set; }
    public TimeSpan MqttUnhealthyAfter { get; set; }
    public double BudgetDegradedBelowPercent { get; set; }
}
