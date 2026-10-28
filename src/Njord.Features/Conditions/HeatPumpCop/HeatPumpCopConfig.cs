using Njord.Core.Configuration;

namespace Njord.Features.Conditions.HeatPumpCop;

public sealed class HeatPumpCopConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Energy:HeatPumpCop";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double TargetTemperature { get; set; } = 35.0;
    public int ForecastHours { get; set; } = 24;
}
