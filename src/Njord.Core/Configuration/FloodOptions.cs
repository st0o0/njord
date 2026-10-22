namespace Njord.Core.Configuration;

public sealed class FloodOptions : IConfigSection
{
    private const string SectionName = "Njord:Flood";
    static string IConfigSection.SectionName => SectionName;

    public int ForecastDays { get; set; } = 92;

    public bool IncludeEnsemble { get; set; }
}
