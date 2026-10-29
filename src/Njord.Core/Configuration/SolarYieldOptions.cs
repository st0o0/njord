
namespace Njord.Core.Configuration;

public sealed class SolarYieldOptions : IConfigSection
{
    private const string SectionName = "Njord:SolarYield";
    static string IConfigSection.SectionName => SectionName;

    public double PeakKw { get; set; } = 5.0;
    public double SystemEfficiency { get; set; } = 0.8;
    public int Azimuth { get; set; } = 180;
    public int Tilt { get; set; } = 30;
}
