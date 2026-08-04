namespace Njord.Configuration;

public sealed class EnrichmentOptions
{
    public ConsensusOptions Consensus { get; set; } = new();
    public AlertOptions Alerts { get; set; } = new();
    public DerivedOptions Derived { get; set; } = new();
    public TrendOptions Trends { get; set; } = new();
    public IndexOptions Indices { get; set; } = new();
    public HistoryOptions History { get; set; } = new();
}
