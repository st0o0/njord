using Njord.Core.Configuration;

namespace Njord.Features.Conditions.SolarYield;

public sealed class SolarYieldConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:SolarYield";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double PeakKw { get; set; } = 5.0;
    public double SystemEfficiency { get; set; } = 0.8;
}
