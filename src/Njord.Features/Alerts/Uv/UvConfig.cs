using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Uv;

public sealed class UvConfig : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Features:Uv";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [3, 6, 8, 11];

    public void ApplyDefaults()
    {
        if (Thresholds is { Length: 0 }) Thresholds = DefaultThresholds;
    }
}
