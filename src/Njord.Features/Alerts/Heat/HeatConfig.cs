using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Heat;

public sealed class HeatConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Weather:Heat";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[]? Thresholds { get; set; }
    public double[] EffectiveThresholds => Thresholds ?? [30, 35, 40];
}
