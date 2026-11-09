using Njord.Core.Configuration;

namespace Njord.Features.Observations.DischargeMean;

public sealed class DischargeMeanObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:DischargeMeanObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
