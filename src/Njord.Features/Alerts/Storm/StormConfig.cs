using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Storm;

public sealed class StormConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Forecast:Storm";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[] GustThresholds { get; set; } = [];
    public static double[] DefaultGustThresholds => [17, 25, 33];
}
