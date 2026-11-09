using Njord.Core.Configuration;

namespace Njord.Features.Observations.FloodDischarge;

public sealed class FloodDischargeObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:FloodDischargeObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
