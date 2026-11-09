using Njord.Core.Configuration;

namespace Njord.Features.Observations.SulphurDioxide;

public sealed class SulphurDioxideObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:SulphurDioxideObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
