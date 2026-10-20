namespace Njord.Core.Configuration;

public sealed class FloodOptions
{
    public const string SectionName = "Njord:Flood";

    public int ForecastDays { get; set; } = 92;

    public bool IncludeEnsemble { get; set; }
}
