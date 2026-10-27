using Njord.Core.Configuration;

namespace Njord.Features.Conditions.GranularAqi;

public sealed class GranularAqiConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:AirQuality:GranularAqi";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
