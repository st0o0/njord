using Njord.Core.Configuration;

namespace Njord.Features.Conditions.NightVentilation;

public sealed class NightVentilationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:NightVentilation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double HotDayPeakCelsius { get; set; } = 28.0;
    public double CoolNightLimitCelsius { get; set; } = 20.0;
}
