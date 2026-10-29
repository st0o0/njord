using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Humidity;

public sealed class HumidityConfig : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Features:Humidity";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [16, 21, 24];

    public void ApplyDefaults()
    {
        if (Thresholds is { Length: 0 }) Thresholds = DefaultThresholds;
    }
}
