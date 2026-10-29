using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class ForecastOptions : IConfigSection
{
    private const string SectionName = "Njord:Forecast";
    static string IConfigSection.SectionName => SectionName;

    public string[] Models { get; set; } = [];

    public int ForecastDays { get; set; } = 4;

    public int PastHours { get; set; }
}
