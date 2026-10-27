using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Snow;

public sealed class SnowConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Forecast:Snow";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
}
