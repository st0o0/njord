using Njord.Core.Configuration;

namespace Njord.Features.Alerts.MigraineRisk;

public sealed class MigraineRiskConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:MigraineRisk";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double PressureThreshold6H { get; set; } = 5.0;
    public double PressureThreshold24H { get; set; } = 10.0;
    public double TempSwingThreshold { get; set; } = 8.0;
    public double HumiditySwingThreshold { get; set; } = 30.0;
    public double FoehnDirectionMin { get; set; } = 150.0;
    public double FoehnDirectionMax { get; set; } = 210.0;
    public int ShortLookbackHours { get; set; } = 6;
    public int LongLookbackHours { get; set; } = 24;
    public int FoehnLookbackHours { get; set; } = 3;
}
