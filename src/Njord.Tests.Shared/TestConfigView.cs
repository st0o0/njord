using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;

namespace Njord.Tests.Shared;

public static class TestConfigView
{
    public static ConfigView Create(
        NjordOptions? scope = null,
        PollingOptions? polling = null,
        TestOptionsMonitor<PollingOptions>? pollingMonitor = null) =>
        new(
            new TestOptionsMonitor<NjordOptions>(scope ?? AppSettingsDefaults.Options<NjordOptions>()),
            pollingMonitor ?? new TestOptionsMonitor<PollingOptions>(polling ?? AppSettingsDefaults.Options<PollingOptions>()));
}
