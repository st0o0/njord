using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Snow;

public sealed class SnowConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Forecast:Snow";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[]? Thresholds { get; set; }
    public double[] EffectiveThresholds => Thresholds ?? [1.0, 5.0, 15.0];
}
