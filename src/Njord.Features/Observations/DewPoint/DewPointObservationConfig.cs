using Njord.Core.Configuration;

namespace Njord.Features.Observations.DewPoint;

public sealed class DewPointObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:DewPointObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
