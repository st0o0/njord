using Njord.Compute.Configuration;

namespace Njord.Core.Configuration;

public sealed class EnrichmentOptions
{
    public const string SectionName = "Njord:Enrichment";

    public ConsensusOptions Consensus { get; set; } = new();
    public AlertOptions Alerts { get; set; } = new();
    public DerivedOptions Derived { get; set; } = new();
    public TrendOptions Trends { get; set; } = new();
    public IndexOptions Indices { get; set; } = new();
}
