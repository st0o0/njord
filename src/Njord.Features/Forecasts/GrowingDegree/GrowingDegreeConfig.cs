using Njord.Core.Configuration;

namespace Njord.Features.Forecasts.GrowingDegree;

public sealed class GrowingDegreeConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Lifestyle:GrowingDegree";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double BaseTemperature { get; set; } = 5.0;
}
