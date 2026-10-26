using Microsoft.Extensions.Logging;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;

namespace Njord.Core.Tests.Domain;

public sealed class FetchPlanSpec
{
    private sealed class StubRule(string name, FeatureCategory category, bool enabled, DomainRequirements requirements) : IFeatureRule
    {
        public string Name { get; } = name;

        public FeatureDescriptor Descriptor { get; } = new(OutputKind.Condition, category, name, enabled, requirements);

        public bool Enabled { get; } = enabled;

        public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => null;
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private static readonly string[] Three =
        ["ecmwf_aifs025_ensemble_mean", "ecmwf_ifs025_ensemble_mean", "ukmo_global_ensemble_mean_20km"];

    private static readonly StubRule Frost = new("Frost", FeatureCategory.Weather, true, new(Weather: WeatherParams.Temperature | WeatherParams.WindGusts));
    private static readonly StubRule Pm25 = new("Pm25", FeatureCategory.AirQuality, true, new(AirQuality: AirQualityParams.Pm25));

    private static StubRule Comparison(bool enabled) =>
        new(EffectiveWeatherModels.ComparisonFeatureName, FeatureCategory.Weather, enabled, new(Weather: WeatherParams.Temperature));

    [Fact]
    public void Should_derive_domains_requirements_models_and_weight_in_one_plan()
    {
        var plan = FeatureResolution.BuildFetchPlan([Frost, Pm25], new WeatherOptions { ForecastDays = 4 }, new CapturingLogger());

        Assert.Multiple(
            () => Assert.Equal(new HashSet<DomainKind> { DomainKind.Weather, DomainKind.AirQuality }, plan.Domains.ToHashSet()),
            () => Assert.Equal(
                WeatherParams.Temperature | WeatherParams.WindGusts | WeatherParams.WeatherCode,
                plan.Requirements.Weather),
            () => Assert.Equal(AirQualityParams.Pm25 | AirQualityParams.Pm10, plan.Requirements.AirQuality),
            () => Assert.Equal(["ecmwf_ifs025_ensemble_mean"], plan.Models.Ids),
            () => Assert.Equal(1, plan.WeatherCallWeight));
    }

    [Fact]
    public void Should_plan_nothing_when_no_feature_is_enabled()
    {
        var off = new StubRule("Frost", FeatureCategory.Weather, false, Frost.Descriptor.Requirements);

        var plan = FeatureResolution.BuildFetchPlan([off], new WeatherOptions(), new CapturingLogger());

        Assert.Multiple(
            () => Assert.Empty(plan.Domains),
            () => Assert.Equal(default, plan.Requirements),
            () => Assert.Equal(0, plan.WeatherCallWeight));
    }

    [Fact]
    public void Should_multiply_the_weather_weight_by_the_models_while_comparison_is_enabled()
    {
        var plan = FeatureResolution.BuildFetchPlan([Frost, Comparison(true)], new WeatherOptions { Models = Three }, new CapturingLogger());

        Assert.Multiple(
            () => Assert.Equal(Three, plan.Models.Ids),
            () => Assert.Equal(3, plan.WeatherCallWeight));
    }

    [Fact]
    public void Should_log_the_ignored_models_when_comparison_is_disabled()
    {
        var logger = new CapturingLogger();

        var plan = FeatureResolution.BuildFetchPlan([Frost, Comparison(false)], new WeatherOptions { Models = Three }, logger);

        var message = Assert.Single(logger.Messages);
        Assert.Multiple(
            () => Assert.Equal([Three[0]], plan.Models.Ids),
            () => Assert.Contains("ecmwf_ifs025_ensemble_mean, ukmo_global_ensemble_mean_20km", message),
            () => Assert.Contains(Three[0], message));
    }

    [Fact]
    public void Should_not_log_when_every_configured_model_is_effective()
    {
        var logger = new CapturingLogger();

        FeatureResolution.BuildFetchPlan([Frost], new WeatherOptions(), logger);

        Assert.Empty(logger.Messages);
    }
}
