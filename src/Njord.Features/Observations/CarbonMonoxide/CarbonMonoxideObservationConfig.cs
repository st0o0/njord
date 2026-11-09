using Njord.Core.Configuration;

namespace Njord.Features.Observations.CarbonMonoxide;

public sealed class CarbonMonoxideObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:CarbonMonoxideObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
