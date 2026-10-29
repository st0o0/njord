using Njord.Core.Configuration;

namespace Njord.Features.Alerts.PollenBlast;

public sealed class PollenBlastConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:PollenBlast";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double TemperatureThreshold { get; set; } = 15.0;
    public double WindSpeedThreshold { get; set; } = 1.4;
    public double PollenDelta { get; set; } = 10.0;
    public double PrecipitationThreshold { get; set; } = 1.0;
}
