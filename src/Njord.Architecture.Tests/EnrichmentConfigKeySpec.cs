using Microsoft.Extensions.DependencyInjection;
using Njord.AirQuality;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Flood;
using Njord.Marine;
using Njord.Tests.Shared;
using Njord.Weather.Alerts;

namespace Njord.Architecture.Tests;

public sealed class EnrichmentConfigKeySpec
{
    private static IReadOnlyList<IDomainHandler> RegisteredHandlers()
    {
        using var provider = FeatureServices.Build(TimeProvider.System, services =>
        {
            services.AddAlertOptions(AppSettingsDefaults.Configuration());
            services.AddWeatherDomain().AddAirQualityDomain().AddFloodDomain().AddMarineDomain();
        });
        return [.. provider.GetServices<IDomainHandler>()];
    }

    [Fact]
    public void Every_registered_handler_has_a_config_key_matching_an_enrichment_option()
    {
        var handlers = RegisteredHandlers();

        Assert.NotEmpty(handlers);
        Assert.All(handlers, handler => Assert.True(
            EnrichmentOptions.IsKnownKey(handler.ConfigKey),
            $"{handler.GetType().Name} declares ConfigKey '{handler.ConfigKey}' which is not an EnrichmentOptions property."));
    }

    [Fact]
    public void Config_keys_are_unique_and_every_enrichment_option_has_a_handler()
    {
        var handlerKeys = RegisteredHandlers().Select(h => h.ConfigKey).ToList();

        Assert.Multiple(
            () => Assert.Equal(handlerKeys.Count, handlerKeys.Distinct().Count()),
            () => Assert.Equal(EnrichmentOptions.Keys.Order(), handlerKeys.Order()));
    }
}
