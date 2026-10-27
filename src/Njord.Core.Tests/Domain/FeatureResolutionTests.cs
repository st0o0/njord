using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Forecast;
using Njord.Forecast.Marine;
using Njord.Messages.Pipeline;

namespace Njord.Core.Tests.Domain;

public sealed class FeatureResolutionTests
{
    private sealed class StubRule(string name, FeatureCategory category, bool enabled, DomainRequirements requirements) : IFeatureRule
    {
        public string Name { get; } = name;

        public FeatureDescriptor Descriptor { get; } = new(OutputKind.Condition, category, name, enabled, requirements);

        public bool Enabled { get; } = enabled;

        public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => null;
    }

    private static readonly StubRule Frost = new("Frost", FeatureCategory.Forecast, true, new(Forecast: ForecastParams.Temperature | ForecastParams.WindGusts));
    private static readonly StubRule Commute = new("Commute", FeatureCategory.Lifestyle, true, new(Forecast: ForecastParams.Temperature | ForecastParams.Precipitation));
    private static readonly StubRule Pollen = new("Pollen", FeatureCategory.AirQuality, true, new(AirQuality: AirQualityParams.BirchPollen));
    private static readonly StubRule Pm25 = new("Pm25", FeatureCategory.AirQuality, true, new(AirQuality: AirQualityParams.Pm25));

    private static StubRule FloodRisk(bool enabled) => new("FloodRisk", FeatureCategory.Flood, enabled, new(Flood: FloodParams.Discharge));

    private static StubRule Swimming(bool enabled) => new("Swimming", FeatureCategory.Marine, enabled, new(Marine: MarineParams.WaveHeight));

    private static StubRule Off(StubRule rule) => new(rule.Name, rule.Descriptor.Category, false, rule.Descriptor.Requirements);

    [Fact]
    public void Should_require_weather_and_air_quality_for_the_default_features()
    {
        var domains = FeatureResolution.RequiredDomains([Frost, Commute, Pollen, Pm25, FloodRisk(false), Swimming(false)]);

        Assert.Equal(new HashSet<DomainKind> { DomainKind.Forecast, DomainKind.AirQuality }, domains.ToHashSet());
    }

    [Fact]
    public void Should_add_the_flood_domain_when_flood_risk_is_enabled()
    {
        var domains = FeatureResolution.RequiredDomains([Frost, Pollen, FloodRisk(true)]);

        Assert.Equal(
            new HashSet<DomainKind> { DomainKind.Forecast, DomainKind.AirQuality, DomainKind.Flood },
            domains.ToHashSet());
    }

    [Fact]
    public void Should_drop_air_quality_when_no_air_quality_feature_is_enabled()
    {
        var domains = FeatureResolution.RequiredDomains([Frost, Off(Pollen), Off(Pm25)]);

        Assert.Equal(new HashSet<DomainKind> { DomainKind.Forecast }, domains.ToHashSet());
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
                ForecastParams.Temperature | ForecastParams.WindGusts | ForecastParams.Precipitation,
                requirements.Forecast),
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

    [Fact]
    public void Should_add_the_weather_code_to_a_required_weather_domain()
    {
        var requirements = FeatureResolution.WithBaseParams(FeatureResolution.RequiredParams([Frost]));

        Assert.Equal(ForecastParams.Temperature | ForecastParams.WindGusts | ForecastParams.WeatherCode, requirements.Forecast);
    }

    [Fact]
    public void Should_add_pm10_to_a_required_air_quality_domain()
    {
        var requirements = FeatureResolution.WithBaseParams(FeatureResolution.RequiredParams([Pm25]));

        Assert.Equal(AirQualityParams.Pm25 | AirQualityParams.Pm10, requirements.AirQuality);
    }

    [Fact]
    public void Should_not_add_a_domain_through_base_params()
    {
        var requirements = FeatureResolution.WithBaseParams(FeatureResolution.RequiredParams([Frost]));

        Assert.Multiple(
            () => Assert.Equal(AirQualityParams.None, requirements.AirQuality),
            () => Assert.False(requirements.Requires(DomainKind.AirQuality)),
            () => Assert.False(FeatureResolution.WithBaseParams(default).Requires(DomainKind.Forecast)));
    }
}
