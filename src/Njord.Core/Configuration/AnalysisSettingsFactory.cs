using Njord.Compute.Settings;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Trends;

namespace Njord.Core.Configuration;

public static class AnalysisSettingsFactory
{
    // The forecast scope (locations, horizons) is restart-required, so callers pass the scope the
    // running pipeline was started with, while the analysis sections may be the reloaded ones.
    public static AnalysisSettings Create(
        NjordOptions scope,
        ConsensusOptions consensus,
        AlertOptions alerts,
        IndexOptions indices,
        TrendOptions trends,
        DerivedOptions derived) =>
        new(
            consensus.ToSettings(),
            alerts.ToSettings(),
            indices.ToSettings(scope.Locations.Select(l => l.Name)),
            trends.ToSettings(),
            derived.ToSettings(scope.Horizons));
}
