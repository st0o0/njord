using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Trends;

namespace Njord.Core.Configuration;

// The persisted configuration as it is right now, read per call by request handlers outside actors.
// It never subscribes to changes; the ConfigManager is the only change subscriber.
public sealed class ConfigView(
    IOptionsMonitor<NjordOptions> scope,
    IOptionsMonitor<PollingOptions> polling,
    IOptionsMonitor<ConsensusOptions> consensus,
    IOptionsMonitor<AlertOptions> alerts,
    IOptionsMonitor<DerivedOptions> derived,
    IOptionsMonitor<TrendOptions> trends,
    IOptionsMonitor<IndexOptions> indices)
{
    public NjordOptions Scope => scope.CurrentValue;
    public PollingOptions Polling => polling.CurrentValue;
    public ConsensusOptions Consensus => consensus.CurrentValue;
    public AlertOptions Alerts => alerts.CurrentValue;
    public DerivedOptions Derived => derived.CurrentValue;
    public TrendOptions Trends => trends.CurrentValue;
    public IndexOptions Indices => indices.CurrentValue;
}
