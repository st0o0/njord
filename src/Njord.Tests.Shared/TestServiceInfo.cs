using Njord.Alerts.Configuration;
using Njord.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Derived.Configuration;
using Njord.Indices.Configuration;
using Njord.Messages.Config;
using Njord.Pipeline.Configuration;
using Njord.Trends.Configuration;

namespace Njord.Tests.Shared;

public static class TestServiceInfo
{
    public static ServiceInfo Create(
        NjordOptions? scope = null,
        PollingOptions? polling = null,
        ConsensusOptions? consensus = null,
        AlertOptions? alerts = null,
        DerivedOptions? derived = null,
        TrendOptions? trends = null,
        IndexOptions? indices = null) =>
        ServiceInfoMappings.ToServiceInfo(
            scope ?? AppSettingsDefaults.Options<NjordOptions>(),
            polling ?? AppSettingsDefaults.Options<PollingOptions>(),
            consensus ?? AppSettingsDefaults.Options<ConsensusOptions>(),
            alerts ?? AppSettingsDefaults.Options<AlertOptions>(),
            derived ?? AppSettingsDefaults.Options<DerivedOptions>(),
            trends ?? AppSettingsDefaults.Options<TrendOptions>(),
            indices ?? AppSettingsDefaults.Options<IndexOptions>());
}
