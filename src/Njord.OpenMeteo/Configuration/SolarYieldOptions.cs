using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class SolarYieldOptions : IConfigSection
{
    public const string SectionName = "Njord:SolarYield";
    static string IConfigSection.SectionName => SectionName;

    public double PeakKw { get; set; } = 5.0;
    public int Azimuth { get; set; } = 180;
    public int Tilt { get; set; } = 30;
}
