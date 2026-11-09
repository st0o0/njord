using Njord.Core.Configuration;

namespace Njord.Features.Observations.Pm10;

public sealed class Pm10ObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Pm10Observation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
