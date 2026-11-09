using Njord.Core.Configuration;

namespace Njord.Features.Observations.WavePeriod;

public sealed class WavePeriodObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:WavePeriodObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
