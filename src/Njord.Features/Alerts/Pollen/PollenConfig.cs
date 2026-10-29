using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Pollen;

public sealed class PollenConfig : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Features:Pollen";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [20, 50, 100];

    public void ApplyDefaults()
    {
        if (Thresholds is { Length: 0 }) Thresholds = DefaultThresholds;
    }
}
