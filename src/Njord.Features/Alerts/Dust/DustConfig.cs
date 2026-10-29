using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Dust;

public sealed class DustConfig : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Features:Dust";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public int WindowHours { get; set; } = 24;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [50, 150, 500];

    public void ApplyDefaults()
    {
        if (Thresholds is { Length: 0 }) Thresholds = DefaultThresholds;
    }
}
