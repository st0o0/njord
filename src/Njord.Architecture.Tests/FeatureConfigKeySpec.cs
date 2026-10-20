using Microsoft.Extensions.DependencyInjection;
using Njord.AirQuality;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Flood;
using Njord.Marine;
using Njord.Tests.Shared;
using Njord.Weather.Alerts;

namespace Njord.Architecture.Tests;

public sealed class FeatureConfigKeySpec
{
    private static IReadOnlyList<FeatureDescriptor> RegisteredFeatures()
    {
        using var provider = FeatureServices.Build(TimeProvider.System, services =>
        {
            services.AddAlertOptions(AppSettingsDefaults.Configuration());
            services.AddWeatherDomain().AddAirQualityDomain().AddFloodDomain().AddMarineDomain();
        });
        return FeatureResolution.AllFeatures(provider.GetServices<IDomainHandler>());
    }

    [Fact]
    public void Every_feature_kind_is_declared_by_exactly_one_registered_feature()
    {
        var kinds = RegisteredFeatures().Select(feature => feature.Kind).ToList();

        Assert.Multiple(
            () => Assert.NotEmpty(kinds),
            () => Assert.Equal(kinds.Count, kinds.Distinct().Count()),
            () => Assert.Equal(Enum.GetValues<FeatureKind>().Order(), kinds.Order()));
    }

    [Fact]
    public void Every_registered_feature_has_a_distinct_config_key()
    {
        var keys = RegisteredFeatures().Select(feature => feature.Kind.ToConfigKey()).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }
}
