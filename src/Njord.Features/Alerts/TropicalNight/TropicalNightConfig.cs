using Njord.Core.Configuration;

namespace Njord.Features.Alerts.TropicalNight;

public sealed class TropicalNightConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Weather:TropicalNight";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[]? Thresholds { get; set; }
    public double[] EffectiveThresholds => Thresholds ?? [20, 23, 25];
}
