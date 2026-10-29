using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Dust;

public sealed class DustConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:AirQuality:Dust";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [50, 150, 500];
}
