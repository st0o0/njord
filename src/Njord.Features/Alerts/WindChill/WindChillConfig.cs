using Njord.Core.Configuration;

namespace Njord.Features.Alerts.WindChill;

public sealed class WindChillConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Forecast:WindChill";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [];
    public static double[] DefaultThresholds => [-10, -20, -30];
}
