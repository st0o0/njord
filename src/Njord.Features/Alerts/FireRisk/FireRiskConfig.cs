using Njord.Core.Configuration;

namespace Njord.Features.Alerts.FireRisk;

public sealed class FireRiskConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Lifestyle:FireRisk";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double TemperatureThreshold { get; set; } = 30.0;
    public double HumidityThreshold { get; set; } = 30.0;
    public double WindSpeedThreshold { get; set; } = 5.6;
    public double SoilMoistureThreshold { get; set; } = 0.10;
    public double RedThreshold { get; set; } = 0.7;
    public double OrangeThreshold { get; set; } = 0.4;
    public double YellowThreshold { get; set; } = 0.2;
}
