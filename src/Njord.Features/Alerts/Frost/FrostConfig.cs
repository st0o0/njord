using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Frost;

public sealed class FrostConfig : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Features:Frost";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [0, -5, -15];

    public void ApplyDefaults()
    {
        if (Thresholds is { Length: 0 }) Thresholds = DefaultThresholds;
    }
}
