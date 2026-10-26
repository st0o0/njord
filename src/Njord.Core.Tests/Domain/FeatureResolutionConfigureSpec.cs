using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Core.Tests.Domain;

public sealed class FeatureResolutionConfigureSpec
{
    private sealed class StubRule(string name, FeatureCategory category, bool enabled, DomainRequirements requirements) : IFeatureRule
    {
        public FeatureDescriptor Descriptor { get; } = new(OutputKind.Condition, category, name, enabled, requirements);
        public bool Enabled { get; } = enabled;
        public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => null;
    }

    private static readonly StubRule Frost = new("Frost", FeatureCategory.Weather, true,
        new(Weather: WeatherParams.Temperature | WeatherParams.WindGusts));

    private static readonly StubRule Pollen = new("Pollen", FeatureCategory.AirQuality, true,
        new(AirQuality: AirQualityParams.BirchPollen));

    private static StubRule Comparison(bool enabled) => new(
        FeatureResolution.ComparisonFeatureName, FeatureCategory.Weather, enabled,
        new(Weather: WeatherParams.Temperature));

    private static readonly string[] ThreeModels = ["model_a", "model_b", "model_c"];

    [Fact]
    public void Enabled_features_produce_correct_requirements()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost, Pollen], new WeatherOptions());

        Assert.True(options.Requirements.Requires(DomainKind.Weather));
        Assert.True(options.Requirements.Requires(DomainKind.AirQuality));
        Assert.False(options.Requirements.Requires(DomainKind.Flood));
    }

    [Fact]
    public void Domains_match_required_domains()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost, Pollen], new WeatherOptions());

        Assert.Equal([DomainKind.Weather, DomainKind.AirQuality], options.Domains.Order().ToList());
    }

    [Fact]
    public void No_features_produce_defaults()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [], new WeatherOptions());

        Assert.Equal(default, options.Requirements);
        Assert.Empty(options.Domains);
        Assert.Single(options.WeatherModels);
        Assert.Equal(WeatherOptions.DefaultModel, options.PrimaryWeatherModel);
    }

    [Fact]
    public void Model_comparison_enabled_includes_all_configured_models()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost, Comparison(true)],
            new WeatherOptions { Models = ThreeModels });

        Assert.Equal(3, options.WeatherModels.Length);
        Assert.Equal("model_a", options.PrimaryWeatherModel);
    }

    [Fact]
    public void Model_comparison_disabled_uses_primary_only()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost, Comparison(false)],
            new WeatherOptions { Models = ThreeModels });

        Assert.Single(options.WeatherModels);
        Assert.Equal("model_a", options.PrimaryWeatherModel);
    }

    [Fact]
    public void Base_params_are_applied()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost], new WeatherOptions());

        Assert.True(options.Requirements.AllWeather.HasFlag(WeatherParams.WeatherCode));
    }

    [Fact]
    public void Base_params_do_not_add_unrequired_domains()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost], new WeatherOptions());

        Assert.False(options.Requirements.Requires(DomainKind.AirQuality));
    }
}
