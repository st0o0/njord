using Njord.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Domain;
using Njord.Messages.Config;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public static class TestServiceInfo
{
    public static ServiceInfo Create(
        NjordOptions? scope = null,
        PollingOptions? polling = null,
        AlertOptions? alerts = null,
        IEnumerable<FeatureDescriptor>? enabledFeatures = null) =>
        ServiceInfoMappings.ToServiceInfo(
            scope ?? AppSettingsDefaults.Options<NjordOptions>(),
            polling ?? AppSettingsDefaults.Options<PollingOptions>(),
            alerts ?? AppSettingsDefaults.Options<AlertOptions>(),
            enabledFeatures ?? [Feature(FeatureKind.CommuteSafety), Feature(FeatureKind.PollenAlert), Feature(FeatureKind.Pm25Alert)]);

    public static FeatureDescriptor Feature(FeatureKind kind) =>
        new(kind, FeatureCategory.Lifestyle, kind.ToConfigKey(), DefaultEnabled: false, default);
}
