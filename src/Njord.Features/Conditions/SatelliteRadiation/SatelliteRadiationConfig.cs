using Njord.Core.Configuration;

namespace Njord.Features.Conditions.SatelliteRadiation;

public sealed class SatelliteRadiationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Energy:SatelliteRadiation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
