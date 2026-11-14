using Njord.Core.Configuration;

namespace Njord.Features.Trends.ConfidenceTrend;

public sealed class ConfidenceTrendConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:ConfidenceTrend";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double ChangeThreshold { get; set; } = 0.20;
}
