using Njord.Core.Configuration;

namespace Njord.Features.Alerts.SmogRisk;

public sealed class SmogRiskConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:SmogRisk";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double BoundaryLayerThresholdM { get; set; } = 500;
    public double Pm25ThresholdUgm3 { get; set; } = 25.0;
}
