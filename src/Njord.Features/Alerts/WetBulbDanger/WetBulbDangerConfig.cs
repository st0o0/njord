using Njord.Core.Configuration;

namespace Njord.Features.Alerts.WetBulbDanger;

public sealed class WetBulbDangerConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Forecast:WetBulbDanger";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double[]? Thresholds { get; set; }
    public double[] EffectiveThresholds => Thresholds ?? [26, 28, 32];
}
