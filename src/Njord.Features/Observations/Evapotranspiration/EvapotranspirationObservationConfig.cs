using Njord.Core.Configuration;

namespace Njord.Features.Observations.Evapotranspiration;

public sealed class EvapotranspirationObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:EvapotranspirationObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
