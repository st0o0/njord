using Njord.Core.Configuration;

namespace Njord.Features.Alerts.TropicalNight;

public sealed class TropicalNightConfig : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Features:TropicalNight";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public int WindowHours { get; set; } = 24;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [20, 23, 25];

    public void ApplyDefaults()
    {
        if (Thresholds is { Length: 0 }) Thresholds = DefaultThresholds;
    }
}
