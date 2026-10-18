using Njord.Alerts.Configuration;
using Njord.Configuration;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Pipeline.Configuration;
using Njord.Trends.Configuration;

namespace Njord.Tests.Shared;

public static class TestAnalysisConfigView
{
    public static AnalysisConfigView Create(
        ConsensusOptions? consensus = null,
        AlertOptions? alerts = null,
        DerivedOptions? derived = null,
        TrendOptions? trends = null,
        IndexOptions? indices = null) =>
        new(
            new TestOptionsMonitor<ConsensusOptions>(consensus ?? AppSettingsDefaults.Options<ConsensusOptions>()),
            new TestOptionsMonitor<AlertOptions>(alerts ?? AppSettingsDefaults.Options<AlertOptions>()),
            new TestOptionsMonitor<DerivedOptions>(derived ?? AppSettingsDefaults.Options<DerivedOptions>()),
            new TestOptionsMonitor<TrendOptions>(trends ?? AppSettingsDefaults.Options<TrendOptions>()),
            new TestOptionsMonitor<IndexOptions>(indices ?? AppSettingsDefaults.Options<IndexOptions>()));
}
