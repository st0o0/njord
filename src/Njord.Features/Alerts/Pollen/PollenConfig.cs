using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Pollen;

public sealed class PollenConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:AirQuality:Pollen";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
}
