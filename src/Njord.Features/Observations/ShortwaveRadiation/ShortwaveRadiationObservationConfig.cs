using Njord.Core.Configuration;

namespace Njord.Features.Observations.ShortwaveRadiation;

public sealed class ShortwaveRadiationObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:ShortwaveRadiationObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
