using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class SatelliteOptions : IConfigSection
{
    public const string SectionName = "Njord:Satellite";
    static string IConfigSection.SectionName => SectionName;

    // Hours of observed radiation requested per fetch; the blend only needs the recent hours.
    public int PastHours { get; set; } = 6;
}
