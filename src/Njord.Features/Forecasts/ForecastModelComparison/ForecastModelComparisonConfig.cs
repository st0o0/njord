using Njord.Core.Configuration;

namespace Njord.Features.Forecasts.ForecastModelComparison;

public sealed class ForecastModelComparisonConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:ForecastModelComparison";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
}
