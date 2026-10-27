using Njord.Core.Configuration;

namespace Njord.Features.Conditions.SoilMoisture;

public sealed class SoilMoistureConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Lifestyle:SoilMoisture";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double DryThreshold { get; set; } = 0.15;
    public double WetThreshold { get; set; } = 0.35;
}
