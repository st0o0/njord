using Njord.Core.Configuration;

namespace Njord.Features.Alerts.WetBulbDanger;

public sealed class WetBulbDangerConfig : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Features:WetBulbDanger";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public int WindowHours { get; set; } = 24;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [26, 28, 32];

    public void ApplyDefaults()
    {
        if (Thresholds is { Length: 0 }) Thresholds = DefaultThresholds;
    }
}
