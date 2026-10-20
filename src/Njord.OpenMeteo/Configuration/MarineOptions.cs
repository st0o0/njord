using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class MarineOptions : IConfigSection
{
    public const string SectionName = "Njord:Marine";
    static string IConfigSection.SectionName => SectionName;

    public int ForecastDays { get; set; } = 8;
}
