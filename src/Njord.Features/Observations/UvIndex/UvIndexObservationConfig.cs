using Njord.Core.Configuration;

namespace Njord.Features.Observations.UvIndex;

public sealed class UvIndexObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:UvIndexObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
