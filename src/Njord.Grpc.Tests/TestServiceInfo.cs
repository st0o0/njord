using Njord.Configuration;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Core.Domain;
using Njord.Messages.Config;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public static class TestServiceInfo
{
    public static ServiceInfo Create(
        NjordOptions? scope = null,
        PollingOptions? polling = null,
        AlertOptions? alerts = null,
        IEnumerable<IFeature>? enabledFeatures = null) =>
        ServiceInfoMappings.ToServiceInfo(
            scope ?? AppSettingsDefaults.Options<NjordOptions>(),
            polling ?? AppSettingsDefaults.Options<PollingOptions>(),
            alerts ?? AppSettingsDefaults.Options<AlertOptions>(),
            enabledFeatures ?? [Feature(FeatureCategory.Lifestyle, "Commute"), Feature(FeatureCategory.AirQuality, "Pollen"), Feature(FeatureCategory.AirQuality, "Pm25")]);

    public static IFeature Feature(FeatureCategory category, string name) =>
        new TestFeature(name, new FeatureDescriptor(OutputKind.Score, category, DefaultEnabled: false, default));

    private sealed record TestFeature(string Name, FeatureDescriptor Feature) : IFeature;
}
