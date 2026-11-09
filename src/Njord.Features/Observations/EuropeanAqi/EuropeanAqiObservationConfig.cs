using Njord.Core.Configuration;

namespace Njord.Features.Observations.EuropeanAqi;

public sealed class EuropeanAqiObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:EuropeanAqiObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
