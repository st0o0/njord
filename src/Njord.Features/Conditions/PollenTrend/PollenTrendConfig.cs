using Njord.Core.Configuration;

namespace Njord.Features.Conditions.PollenTrend;

public sealed class PollenTrendConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:AirQuality:PollenTrend";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
