using Njord.Core.Configuration;

namespace Njord.Features.Observations.SoilMoisture;

public sealed class SoilMoistureObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:SoilMoistureObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
