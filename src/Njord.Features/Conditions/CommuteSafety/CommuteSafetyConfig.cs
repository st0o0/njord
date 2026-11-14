using Njord.Core.Configuration;

namespace Njord.Features.Conditions.CommuteSafety;

public sealed class CommuteSafetyConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Commute";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double IceTemperatureCelsius { get; set; } = 1.0;
    public double DewPointSpreadCelsius { get; set; } = 2.0;
    public double FogHumidityPercent { get; set; } = 95.0;
    public double HeavyRainMmH { get; set; } = 5.0;
    public double StrongWindKmh { get; set; } = 60.0;
}
