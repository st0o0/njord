using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Fog;

public sealed class FogConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Fog";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public int PersistentHours { get; set; } = 4;
}
