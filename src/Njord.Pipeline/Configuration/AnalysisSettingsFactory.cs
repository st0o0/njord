using Njord.Alerts.Configuration;
using Njord.Core.Configuration;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Messages.Config;
using Njord.Trends.Configuration;

namespace Njord.Pipeline.Configuration;

public static class AnalysisSettingsFactory
{
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
