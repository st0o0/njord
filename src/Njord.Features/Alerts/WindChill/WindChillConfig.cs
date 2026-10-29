using Njord.Core.Configuration;

namespace Njord.Features.Alerts.WindChill;

public sealed class WindChillConfig : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Features:WindChill";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public int WindowHours { get; set; } = 24;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [-10, -20, -30];

    public void ApplyDefaults()
    {
        if (Thresholds is { Length: 0 }) Thresholds = DefaultThresholds;
    }
}
