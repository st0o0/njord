using Njord.Core.Configuration;

namespace Njord.Features.Observations.WaveHeight;

public sealed class WaveHeightObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:WaveHeightObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
