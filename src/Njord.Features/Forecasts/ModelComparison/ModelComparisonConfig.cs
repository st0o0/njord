using Njord.Core.Configuration;

namespace Njord.Features.Forecasts.ModelComparison;

public sealed class ModelComparisonConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:ModelComparison";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
