using Njord.Core.Configuration;

namespace Njord.Features.Alerts.RainProbability;

public sealed class RainProbabilityConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Weather:RainProbability";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public int WindowHours { get; set; } = 6;
    public double[]? Thresholds { get; set; }
    public double[] EffectiveThresholds => Thresholds ?? [50, 70, 90];
}
