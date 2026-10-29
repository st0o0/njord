using Njord.Core.Configuration;

namespace Njord.Features.Alerts.CompoundHeatStress;

public sealed class CompoundHeatStressConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:CompoundHeatStress";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double WetBulbWarning { get; set; } = 28;
    public double WetBulbSevere { get; set; } = 32;
    public double WetBulbExtreme { get; set; } = 35;
    public double ApparentTempThreshold { get; set; } = 40;
    public double Pm25Threshold { get; set; } = 25;
    public int BoundaryLayerLow { get; set; } = 500;
}
