using Njord.Core.Configuration;

namespace Njord.Features.Alerts.SwimmingConditions;

public sealed class SwimmingConditionsConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Swimming";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public int WindowHours { get; set; } = 24;
    public double ModerateWaveHeightM { get; set; } = 1.0;
    public double DangerousWaveHeightM { get; set; } = 2.0;
    public double ModerateWaterTempCelsius { get; set; } = 16.0;
    public double DangerousWaterTempCelsius { get; set; } = 10.0;
}
