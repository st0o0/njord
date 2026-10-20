using Njord.Core.Configuration;

namespace Njord.Features.Forecasts.ModelComparison;

public sealed class ModelComparisonConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Weather:ModelComparison";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
