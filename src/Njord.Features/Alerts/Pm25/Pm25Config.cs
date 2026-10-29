using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Pm25;

public sealed class Pm25Config : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Features:Pm25";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [15, 25, 50];

    public void ApplyDefaults()
    {
        if (Thresholds is { Length: 0 }) Thresholds = DefaultThresholds;
    }
}
