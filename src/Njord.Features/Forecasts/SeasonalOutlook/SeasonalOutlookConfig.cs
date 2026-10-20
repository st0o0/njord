using Njord.Core.Configuration;

namespace Njord.Features.Forecasts.SeasonalOutlook;

public sealed class SeasonalOutlookConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Insight:SeasonalOutlook";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
