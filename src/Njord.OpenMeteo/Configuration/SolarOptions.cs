using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class SolarOptions : IConfigSection
{
    private const string SectionName = "Njord:Solar";
    static string IConfigSection.SectionName => SectionName;

    public int Tilt { get; set; } = 30;
    public int Azimuth { get; set; } = 180;
}
