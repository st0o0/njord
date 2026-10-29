using Njord.Core.Configuration;

namespace Njord.Features.Alerts.FloodRisk;

public sealed class FloodRiskConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:FloodRisk";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double MedianFactor { get; set; } = 2.0;
    public int ForecastDays { get; set; } = 7;
}
