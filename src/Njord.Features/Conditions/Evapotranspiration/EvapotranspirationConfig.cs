using Njord.Core.Configuration;

namespace Njord.Features.Conditions.Evapotranspiration;

public sealed class EvapotranspirationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Evapotranspiration";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double LowMm { get; set; } = 2.0;
    public double HighMm { get; set; } = 5.0;
}
