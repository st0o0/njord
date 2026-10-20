using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Messages.Pipeline;

namespace Njord.Core.Tests.Domain;

public sealed class EnrichmentResolutionSpec
{
    private sealed class StubHandler(string configKey, params DomainKind[] triggers) : IDomainHandler
    {
        public string ConfigKey { get; } = configKey;

        public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind>(triggers);

        public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time) => [];
    }

    private static readonly IDomainHandler[] All =
    [
        new StubHandler("WeatherAlerts", DomainKind.Weather),
        new StubHandler("CommuteSafety", DomainKind.Weather),
        new StubHandler("PollenAlert", DomainKind.AirQuality),
        new StubHandler("Pm25Alert", DomainKind.AirQuality),
        new StubHandler("FloodRisk", DomainKind.Flood),
        new StubHandler("SwimmingConditions", DomainKind.Marine),
    ];

    [Fact]
    public void Should_require_weather_and_air_quality_for_the_default_enrichments()
    {
        var domains = EnrichmentResolution.RequiredDomains(new EnrichmentOptions(), All);

        Assert.Equal(new HashSet<DomainKind> { DomainKind.Weather, DomainKind.AirQuality }, domains.ToHashSet());
    }

    [Fact]
    public void Should_add_the_flood_domain_when_flood_risk_is_enabled()
    {
        var domains = EnrichmentResolution.RequiredDomains(new EnrichmentOptions { FloodRisk = true }, All);

        Assert.Equal(
            new HashSet<DomainKind> { DomainKind.Weather, DomainKind.AirQuality, DomainKind.Flood },
            domains.ToHashSet());
    }

    [Fact]
    public void Should_drop_air_quality_when_no_air_quality_enrichment_is_enabled()
    {
        var options = new EnrichmentOptions { PollenAlert = false, Pm25Alert = false };

        var domains = EnrichmentResolution.RequiredDomains(options, All);

        Assert.Equal(new HashSet<DomainKind> { DomainKind.Weather }, domains.ToHashSet());
    }

    [Fact]
    public void Should_keep_only_enabled_handlers()
    {
        var options = new EnrichmentOptions { CommuteSafety = false, SwimmingConditions = true };

        var keys = EnrichmentResolution.EnabledHandlers(options, All).Select(h => h.ConfigKey);

        Assert.Equal(["WeatherAlerts", "PollenAlert", "Pm25Alert", "SwimmingConditions"], keys);
    }
}
