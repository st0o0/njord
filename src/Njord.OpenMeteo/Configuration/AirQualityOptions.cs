using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class AirQualityOptions : IConfigSection
{
    public const string SectionName = "Njord:AirQuality";
    static string IConfigSection.SectionName => SectionName;

    public int ForecastDays { get; set; } = 5;

    public AirQualityRegion Region { get; set; } = AirQualityRegion.Global;
}
