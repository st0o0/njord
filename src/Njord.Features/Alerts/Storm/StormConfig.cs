using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Storm;

public sealed class StormConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Weather:Storm";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[]? GustThresholds { get; set; }
    public double[] EffectiveGustThresholds => GustThresholds ?? [17, 25, 33];
}
