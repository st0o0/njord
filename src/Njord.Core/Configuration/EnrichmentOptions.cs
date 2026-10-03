using Njord.Enrichment;

namespace Njord.Configuration;

public sealed class EnrichmentOptions
{
    public ConsensusOptions Consensus { get; set; } = new();
    public AlertOptions Alerts { get; set; } = new();
    public DerivedOptions Derived { get; set; } = new();
    public TrendOptions Trends { get; set; } = new();
    public IndexOptions Indices { get; set; } = new();
    public HistoryOptions History { get; set; } = new();

    public bool IsEnabled(string typeName) => typeName switch
    {
        EnrichmentTypeNames.Consensus => Consensus.Enabled,
        EnrichmentTypeNames.Alerts => Alerts.Enabled,
        EnrichmentTypeNames.Derived => Derived.Enabled,
        EnrichmentTypeNames.Trends => Trends.Enabled,
        EnrichmentTypeNames.Indices => Indices.Enabled,
        EnrichmentTypeNames.History => History.Enabled,
        _ => throw new ArgumentException($"Unknown enrichment type name '{typeName}'.", nameof(typeName)),
    };
}
