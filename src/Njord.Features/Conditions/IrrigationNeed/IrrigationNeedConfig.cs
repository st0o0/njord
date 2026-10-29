using Njord.Core.Configuration;

namespace Njord.Features.Conditions.IrrigationNeed;

public sealed class IrrigationNeedConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:IrrigationNeed";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double WetThresholdPercent { get; set; } = 60.0;
    public double DryThresholdPercent { get; set; } = 25.0;
}
