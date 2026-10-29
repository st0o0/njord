using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Visibility;

public sealed class VisibilityConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Visibility";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [1000, 200, 50];
}
