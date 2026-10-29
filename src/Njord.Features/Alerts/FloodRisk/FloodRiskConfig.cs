using Njord.Core.Configuration;

namespace Njord.Features.Alerts.FloodRisk;

public sealed class FloodRiskConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:FloodRisk";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
