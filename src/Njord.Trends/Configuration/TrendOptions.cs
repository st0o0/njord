namespace Njord.Trends.Configuration;

public sealed class TrendOptions
{
    public const string SectionName = "Njord:Enrichment:Trends";

    public bool Enabled { get; set; } = false;
}
