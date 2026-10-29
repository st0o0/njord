using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Shower;

public sealed class ShowerConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Forecast:Shower";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public int WindowHours { get; set; } = 6;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [3, 5, 10];
}
