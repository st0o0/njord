using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Heat;

public sealed class HeatConfig : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Features:Heat";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public int WindowHours { get; set; } = 24;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [30, 35, 40];

    public void ApplyDefaults()
    {
        if (Thresholds is { Length: 0 }) Thresholds = DefaultThresholds;
    }
}
