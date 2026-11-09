using Njord.Core.Configuration;

namespace Njord.Features.Observations.Pm25;

public sealed class Pm25ObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Pm25Observation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
