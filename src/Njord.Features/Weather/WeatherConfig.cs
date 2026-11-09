using Njord.Core.Configuration;

namespace Njord.Features.Weather;

public sealed class WeatherConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Weather";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
}
