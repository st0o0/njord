using Njord.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Config;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public static class TestServiceInfo
{
    public static ServiceInfo Create(
        NjordOptions? scope = null,
        PollingOptions? polling = null,
        AlertOptions? alerts = null,
        EnrichmentOptions? enrichments = null) =>
        ServiceInfoMappings.ToServiceInfo(
            scope ?? AppSettingsDefaults.Options<NjordOptions>(),
            polling ?? AppSettingsDefaults.Options<PollingOptions>(),
            alerts ?? AppSettingsDefaults.Options<AlertOptions>(),
            enrichments ?? AppSettingsDefaults.Options<EnrichmentOptions>());
}
