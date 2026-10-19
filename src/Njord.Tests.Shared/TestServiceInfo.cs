using Njord.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Config;

namespace Njord.Tests.Shared;

public static class TestServiceInfo
{
    public static ServiceInfo Create(
        NjordOptions? scope = null,
        PollingOptions? polling = null,
        AlertOptions? alerts = null) =>
        ServiceInfoMappings.ToServiceInfo(
            scope ?? AppSettingsDefaults.Options<NjordOptions>(),
            polling ?? AppSettingsDefaults.Options<PollingOptions>(),
            alerts ?? AppSettingsDefaults.Options<AlertOptions>());
}
