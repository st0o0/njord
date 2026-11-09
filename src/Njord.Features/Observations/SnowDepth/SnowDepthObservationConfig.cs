using Njord.Core.Configuration;

namespace Njord.Features.Observations.SnowDepth;

public sealed class SnowDepthObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:SnowDepthObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
