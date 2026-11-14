using Njord.Core.Configuration;

namespace Njord.Features.Conditions.HeatPumpCop;

public sealed class HeatPumpCopConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:HeatPumpCop";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double TargetTemperature { get; set; } = 35.0;
    public int ForecastHours { get; set; } = 24;
    public double CopIntercept { get; set; } = 6.81;
    public double CopSlope { get; set; } = 0.121;
    public double IdealThreshold { get; set; } = 4.0;
    public double NormalThreshold { get; set; } = 2.5;
    public double InefficientThreshold { get; set; } = 1.5;
}
