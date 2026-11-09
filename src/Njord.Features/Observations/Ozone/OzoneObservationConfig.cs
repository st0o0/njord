using Njord.Core.Configuration;

namespace Njord.Features.Observations.Ozone;

public sealed class OzoneObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:OzoneObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
