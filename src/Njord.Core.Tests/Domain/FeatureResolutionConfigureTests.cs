using Njord.Core.AirQuality;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Core.Tests.Domain;

public sealed class FeatureResolutionConfigureTests
{
    private sealed class StubRule(string name, FeatureCategory category, bool enabled, DomainRequirements requirements) : IFeatureRule
    {
        public FeatureDescriptor Descriptor { get; } = new(OutputKind.Condition, category, name, enabled, requirements);
        public bool Enabled { get; } = enabled;
        public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => null;
    }

    private static readonly StubRule Frost = new("Frost", FeatureCategory.Forecast, true,
        new(Forecast: ForecastParams.Temperature | ForecastParams.WindGusts, Ensemble: EnsembleParams.TemperatureSpread));

    private static readonly StubRule Pollen = new("Pollen", FeatureCategory.AirQuality, true,
        new(AirQuality: AirQualityParams.BirchPollen));

    private static StubRule Comparison(bool enabled) => new(
        FeatureResolution.ComparisonFeatureName, FeatureCategory.Forecast, enabled,
        new(Forecast: ForecastParams.Temperature));

    private static readonly string[] ThreeModels = ["model_a", "model_b", "model_c"];

    [Fact]
    public void Enabled_features_produce_correct_requirements()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost, Pollen], new ForecastOptions { Models = [ForecastOptions.DefaultModel] });

        Assert.True(options.Requirements.Requires(DomainKind.Forecast));
        Assert.True(options.Requirements.Requires(DomainKind.AirQuality));
        Assert.False(options.Requirements.Requires(DomainKind.Flood));
    }

    [Fact]
    public void Domains_match_required_domains()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost, Pollen], new ForecastOptions { Models = [ForecastOptions.DefaultModel] });

        Assert.Equal([DomainKind.Forecast, DomainKind.AirQuality, DomainKind.Ensemble], options.Domains.Order().ToList());
    }

    [Fact]
    public void No_features_produce_defaults()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [], new ForecastOptions { Models = [ForecastOptions.DefaultModel] });

        Assert.Equal(default, options.Requirements);
        Assert.Empty(options.Domains);
        Assert.Single(options.WeatherModels);
        Assert.Equal(ForecastOptions.DefaultModel, options.PrimaryWeatherModel);
    }

    [Fact]
    public void Model_comparison_enabled_includes_all_configured_models()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost, Comparison(true)],
            new ForecastOptions { Models = ThreeModels });

        Assert.Equal(3, options.WeatherModels.Length);
        Assert.Equal("model_a", options.PrimaryWeatherModel);
    }

    [Fact]
    public void Model_comparison_disabled_uses_primary_only()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost, Comparison(false)],
            new ForecastOptions { Models = ThreeModels });

        Assert.Single(options.WeatherModels);
        Assert.Equal("model_a", options.PrimaryWeatherModel);
    }

    [Fact]
    public void Base_params_are_applied()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost], new ForecastOptions { Models = [ForecastOptions.DefaultModel] });

        Assert.True(options.Requirements.AllForecast.HasFlag(ForecastParams.WeatherCode));
    }

    [Fact]
    public void Base_params_do_not_add_unrequired_domains()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost], new ForecastOptions { Models = [ForecastOptions.DefaultModel] });

        Assert.False(options.Requirements.Requires(DomainKind.AirQuality));
    }

    [Fact]
    public void Ensemble_domain_is_required_when_features_need_spread()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost], new ForecastOptions { Models = [ForecastOptions.DefaultModel] });

        Assert.True(options.Requirements.Requires(DomainKind.Ensemble));
        Assert.Contains(DomainKind.Ensemble, options.Domains);
    }

    [Fact]
    public void Ensemble_models_use_primary_only_when_no_comparison()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost], new ForecastOptions { Models = ThreeModels });

        Assert.Single(options.EnsembleModels);
        Assert.Equal("model_a", options.EnsembleModels[0]);
    }

    [Fact]
    public void Ensemble_models_include_all_when_comparison_enabled()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Frost, Comparison(true)],
            new ForecastOptions { Models = ThreeModels });

        Assert.Equal(3, options.EnsembleModels.Length);
    }

    [Fact]
    public void Ensemble_models_empty_when_no_ensemble_features()
    {
        var options = new FeatureRequirementsOptions();

        FeatureResolution.Configure(options, [Pollen], new ForecastOptions { Models = [ForecastOptions.DefaultModel] });

        Assert.True(options.EnsembleModels.IsDefaultOrEmpty);
    }
}
