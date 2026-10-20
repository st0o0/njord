using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Pm25;

public sealed class Pm25Config : IConfigSection
{
    public const string SectionName = "Njord:Features:AirQuality:Pm25";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
}
