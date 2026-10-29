using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Snow;

public sealed class SnowConfig : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Features:Snow";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [1.0, 5.0, 15.0];

    public void ApplyDefaults()
    {
        if (Thresholds is { Length: 0 }) Thresholds = DefaultThresholds;
    }
}
