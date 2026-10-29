using Njord.Core.Configuration;

namespace Njord.Features.Conditions.SolarBatteryStrategy;

public sealed class SolarBatteryStrategyConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:SolarBatteryStrategy";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double PeakKw { get; set; } = 5.0;
    public double BatteryCapacityKwh { get; set; } = 10.0;
    public double SystemEfficiency { get; set; } = 0.85;
}
