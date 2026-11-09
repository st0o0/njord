using Njord.Core.Configuration;

namespace Njord.Features.Observations.OceanCurrentVelocity;

public sealed class OceanCurrentVelocityObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:OceanCurrentVelocityObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
