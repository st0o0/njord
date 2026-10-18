using Microsoft.Extensions.Options;
using Njord.Alerts.Configuration;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Pipeline.Configuration;
using Njord.Trends.Configuration;

namespace Njord.Configuration;

// The persisted analysis sections as they are right now, read per call; the counterpart of ConfigView
// for the options the analysis features own.
public sealed class AnalysisConfigView(
    IOptionsMonitor<ConsensusOptions> consensus,
    IOptionsMonitor<AlertOptions> alerts,
    IOptionsMonitor<DerivedOptions> derived,
    IOptionsMonitor<TrendOptions> trends,
    IOptionsMonitor<IndexOptions> indices)
{
    public ConsensusOptions Consensus => consensus.CurrentValue;
    public AlertOptions Alerts => alerts.CurrentValue;
    public DerivedOptions Derived => derived.CurrentValue;
    public TrendOptions Trends => trends.CurrentValue;
    public IndexOptions Indices => indices.CurrentValue;
}
