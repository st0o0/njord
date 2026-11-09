using Njord.Core.Configuration;

namespace Njord.Features.Observations.SoilTemperature;

public sealed class SoilTemperatureObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:SoilTemperatureObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
