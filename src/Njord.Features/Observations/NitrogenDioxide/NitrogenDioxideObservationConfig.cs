using Njord.Core.Configuration;

namespace Njord.Features.Observations.NitrogenDioxide;

public sealed class NitrogenDioxideObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:NitrogenDioxideObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
