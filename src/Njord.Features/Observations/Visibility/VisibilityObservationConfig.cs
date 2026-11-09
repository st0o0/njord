using Njord.Core.Configuration;

namespace Njord.Features.Observations.Visibility;

public sealed class VisibilityObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:VisibilityObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
