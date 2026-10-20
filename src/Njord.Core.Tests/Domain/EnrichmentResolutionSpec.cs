using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Core.Tests.Domain;

public sealed class EnrichmentResolutionSpec
{
    private sealed class StubHandler(string configKey, DomainRequirements requirements) : IDomainHandler
    {
        public string ConfigKey { get; } = configKey;

        public DomainRequirements Requirements { get; } = requirements;

        public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time) => [];
    }

    private static readonly IDomainHandler[] All =
    [
        new StubHandler("WeatherAlerts", new(Weather: WeatherParams.Temperature | WeatherParams.WindGusts)),
        new StubHandler("CommuteSafety", new(Weather: WeatherParams.Temperature | WeatherParams.Precipitation)),
        new StubHandler("PollenAlert", new(AirQuality: AirQualityParams.BirchPollen)),
        new StubHandler("Pm25Alert", new(AirQuality: AirQualityParams.Pm25)),
        new StubHandler("FloodRisk", new(Flood: FloodParams.Discharge)),
        new StubHandler("SwimmingConditions", new(Marine: MarineParams.WaveHeight)),
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

    [Fact]
    public void Should_union_the_params_of_all_enabled_handlers()
    {
        var requirements = EnrichmentResolution.RequiredParams(new EnrichmentOptions(), All);

        Assert.Multiple(
            () => Assert.Equal(
                WeatherParams.Temperature | WeatherParams.WindGusts | WeatherParams.Precipitation,
                requirements.Weather),
            () => Assert.Equal(AirQualityParams.BirchPollen | AirQualityParams.Pm25, requirements.AirQuality),
            () => Assert.Equal(FloodParams.None, requirements.Flood),
            () => Assert.Equal(MarineParams.None, requirements.Marine));
    }

    [Fact]
    public void Should_include_the_params_of_a_newly_enabled_handler()
    {
        var requirements = EnrichmentResolution.RequiredParams(new EnrichmentOptions { SwimmingConditions = true }, All);

        Assert.Equal(MarineParams.WaveHeight, requirements.Marine);
    }

    [Fact]
    public void Should_require_nothing_when_no_handler_is_enabled()
    {
        var requirements = EnrichmentResolution.RequiredParams(new EnrichmentOptions(), []);

        Assert.Equal(default, requirements);
    }
}
