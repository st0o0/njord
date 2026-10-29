using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Frost;

public sealed class FrostConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Frost";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [0, -5, -15];
}
