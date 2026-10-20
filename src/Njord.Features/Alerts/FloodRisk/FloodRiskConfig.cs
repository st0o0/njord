using Njord.Core.Configuration;

namespace Njord.Features.Alerts.FloodRisk;

public sealed class FloodRiskConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Flood:FloodRisk";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
