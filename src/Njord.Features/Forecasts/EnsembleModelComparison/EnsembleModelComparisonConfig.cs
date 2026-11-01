using Njord.Core.Configuration;

namespace Njord.Features.Forecasts.EnsembleModelComparison;

public sealed class EnsembleModelComparisonConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:EnsembleModelComparison";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
}
