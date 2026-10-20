using Njord.Core.Configuration;

namespace Njord.Features.Alerts.WindChill;

public sealed class WindChillConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Weather:WindChill";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double[]? Thresholds { get; set; }
    public double[] EffectiveThresholds => Thresholds ?? [-10, -20, -30];
}
