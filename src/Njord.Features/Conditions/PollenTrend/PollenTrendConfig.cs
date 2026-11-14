using Njord.Core.Configuration;

namespace Njord.Features.Conditions.PollenTrend;

public sealed class PollenTrendConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:PollenTrend";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double ChangeThreshold { get; set; } = 0.10;
    public double MinimumChange { get; set; } = 1.0;
}
