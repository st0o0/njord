using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Core.Tests.Domain;

public sealed class FeatureResolutionSpec
{
    private sealed class StubRule(string name, FeatureCategory category, bool enabled, DomainRequirements requirements) : IFeatureRule
    {
        public string Name { get; } = name;

        public FeatureDescriptor Descriptor { get; } = new(OutputKind.Condition, category, name, enabled, requirements);

        public bool Enabled { get; } = enabled;

        public IReadOnlyList<PipelineEvent> Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => [];
    }

    private static readonly StubRule Frost = new("Frost", FeatureCategory.Weather, true, new(Weather: WeatherParams.Temperature | WeatherParams.WindGusts));
    private static readonly StubRule Commute = new("Commute", FeatureCategory.Lifestyle, true, new(Weather: WeatherParams.Temperature | WeatherParams.Precipitation));
    private static readonly StubRule Pollen = new("Pollen", FeatureCategory.AirQuality, true, new(AirQuality: AirQualityParams.BirchPollen));
    private static readonly StubRule Pm25 = new("Pm25", FeatureCategory.AirQuality, true, new(AirQuality: AirQualityParams.Pm25));

    private static StubRule FloodRisk(bool enabled) => new("FloodRisk", FeatureCategory.Flood, enabled, new(Flood: FloodParams.Discharge));

    private static StubRule Swimming(bool enabled) => new("Swimming", FeatureCategory.Marine, enabled, new(Marine: MarineParams.WaveHeight));

    private static StubRule Off(StubRule rule) => new(rule.Name, rule.Descriptor.Category, false, rule.Descriptor.Requirements);

    [Fact]
    public void Should_require_weather_and_air_quality_for_the_default_features()
    {
        var domains = FeatureResolution.RequiredDomains([Frost, Commute, Pollen, Pm25, FloodRisk(false), Swimming(false)]);

        Assert.Equal(new HashSet<DomainKind> { DomainKind.Weather, DomainKind.AirQuality }, domains.ToHashSet());
    }

    [Fact]
    public void Should_add_the_flood_domain_when_flood_risk_is_enabled()
    {
        var domains = FeatureResolution.RequiredDomains([Frost, Pollen, FloodRisk(true)]);

        Assert.Equal(
            new HashSet<DomainKind> { DomainKind.Weather, DomainKind.AirQuality, DomainKind.Flood },
            domains.ToHashSet());
    }

    [Fact]
    public void Should_drop_air_quality_when_no_air_quality_feature_is_enabled()
    {
        var domains = FeatureResolution.RequiredDomains([Frost, Off(Pollen), Off(Pm25)]);

        Assert.Equal(new HashSet<DomainKind> { DomainKind.Weather }, domains.ToHashSet());
    }

    [Fact]
    public void Should_list_only_the_enabled_features()
    {
        var names = FeatureResolution.EnabledFeatures([Frost, Off(Commute), Pollen, Swimming(true), FloodRisk(false)])
            .Cast<StubRule>()
            .Select(f => f.Name);

        Assert.Equal(["Frost", "Pollen", "Swimming"], names);
    }

    [Fact]
    public void Should_union_the_params_of_all_enabled_features()
    {
        var requirements = FeatureResolution.RequiredParams([Frost, Commute, Pollen, Pm25, FloodRisk(false)]);

        Assert.Multiple(
            () => Assert.Equal(
                WeatherParams.Temperature | WeatherParams.WindGusts | WeatherParams.Precipitation,
                requirements.Weather),
            () => Assert.Equal(AirQualityParams.BirchPollen | AirQualityParams.Pm25, requirements.AirQuality),
            () => Assert.Equal(FloodParams.None, requirements.Flood),
            () => Assert.Equal(MarineParams.None, requirements.Marine));
    }

    [Fact]
    public void Should_include_the_params_of_a_newly_enabled_feature()
    {
        var requirements = FeatureResolution.RequiredParams([Frost, Swimming(true)]);

        Assert.Equal(MarineParams.WaveHeight, requirements.Marine);
    }

    [Fact]
    public void Should_require_nothing_when_no_feature_is_registered()
    {
        Assert.Equal(default, FeatureResolution.RequiredParams([]));
    }
}
