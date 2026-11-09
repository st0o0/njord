using Njord.Core.Configuration;

namespace Njord.Features.Observations.WindGusts;

public sealed class WindGustsObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:WindGustsObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
