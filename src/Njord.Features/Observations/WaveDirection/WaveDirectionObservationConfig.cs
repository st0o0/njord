using Njord.Core.Configuration;

namespace Njord.Features.Observations.WaveDirection;

public sealed class WaveDirectionObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:WaveDirectionObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
