using Njord.Core.Configuration;

namespace Njord.Features.Alerts.ClearSkyUv;

public sealed class ClearSkyUvConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Lifestyle:ClearSkyUv";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [6, 8, 10];
}
