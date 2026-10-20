using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Humidity;

public sealed class HumidityConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Weather:Humidity";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[]? Thresholds { get; set; }
    public double[] EffectiveThresholds => Thresholds ?? [16, 21, 24];
}
