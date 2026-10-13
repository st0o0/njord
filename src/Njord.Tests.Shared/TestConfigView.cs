using Njord.Core.Configuration;
using Njord.Core.Configuration.Alerts;
using Njord.Core.Configuration.Consensus;
using Njord.Core.Configuration.Derived;
using Njord.Core.Configuration.Indices;
using Njord.Core.Configuration.Polling;
using Njord.Core.Configuration.Trends;

namespace Njord.Tests.Shared;

public static class TestConfigView
{
    public static ConfigView Create(
        NjordOptions? scope = null,
        PollingOptions? polling = null,
        ConsensusOptions? consensus = null,
        AlertOptions? alerts = null,
        DerivedOptions? derived = null,
        TrendOptions? trends = null,
        IndexOptions? indices = null) =>
        new(
            new TestOptionsMonitor<NjordOptions>(scope ?? new NjordOptions()),
            new TestOptionsMonitor<PollingOptions>(polling ?? new PollingOptions()),
            new TestOptionsMonitor<ConsensusOptions>(consensus ?? new ConsensusOptions()),
            new TestOptionsMonitor<AlertOptions>(alerts ?? new AlertOptions()),
            new TestOptionsMonitor<DerivedOptions>(derived ?? new DerivedOptions()),
            new TestOptionsMonitor<TrendOptions>(trends ?? new TrendOptions()),
            new TestOptionsMonitor<IndexOptions>(indices ?? new IndexOptions()));
}
