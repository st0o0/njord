using Njord.Core.Configuration;

namespace Njord.Features.Alerts.MigraineRisk;

public sealed class MigraineRiskConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Forecast:MigraineRisk";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double PressureThreshold6h { get; set; } = 5.0;
    public double PressureThreshold24h { get; set; } = 10.0;
    public double TempSwingThreshold { get; set; } = 8.0;
    public double HumiditySwingThreshold { get; set; } = 30.0;
}
