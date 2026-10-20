namespace Njord.Core.Configuration;

public sealed class SolarYieldOptions
{
    public const string SectionName = "Njord:SolarYield";

    public double PeakKw { get; set; } = 5.0;
    public int Azimuth { get; set; } = 180;
    public int Tilt { get; set; } = 30;
}
