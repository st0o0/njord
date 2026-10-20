using Njord.Core.Configuration;

namespace Njord.Features.Alerts.FloodRain;

public sealed class FloodRainConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Flood:FloodRain";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
