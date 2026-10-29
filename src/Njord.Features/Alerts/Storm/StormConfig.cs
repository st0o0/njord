using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Storm;

public sealed class StormConfig : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Features:Storm";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public int WindowHours { get; set; } = 24;
    public double[] GustThresholds { get; set; } = [];
    public static double[] DefaultGustThresholds => [17, 25, 33];

    public void ApplyDefaults()
    {
        if (GustThresholds is { Length: 0 }) GustThresholds = DefaultGustThresholds;
    }
}
