using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Humidity;

public sealed class HumidityConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Forecast:Humidity";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [16, 21, 24];
}
