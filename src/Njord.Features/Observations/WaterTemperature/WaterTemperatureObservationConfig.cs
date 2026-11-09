using Njord.Core.Configuration;

namespace Njord.Features.Observations.WaterTemperature;

public sealed class WaterTemperatureObservationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:WaterTemperatureObservation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
