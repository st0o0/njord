using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Tests;

public sealed class DomainRequirementsFactoryTests
{
    private sealed class StubRule(bool enabled, DomainRequirements requirements) : IFeatureRule
    {
        public FeatureDescriptor Descriptor { get; } = new(OutputKind.Condition, FeatureCategory.Forecast, "Stub", requirements);
        public bool Enabled { get; } = enabled;
        public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => null;
    }

    [Fact]
    public void Should_aggregate_enabled_rules_with_base_params()
    {
        var factory = new DomainRequirementsFactory(
        [
            new StubRule(true, new(Forecast: ForecastParams.Temperature)),
            new StubRule(true, new(AirQuality: AirQualityParams.Pm25)),
            new StubRule(false, new(Forecast: ForecastParams.WindSpeed)),
        ]);

        var result = factory.Create(string.Empty);

        Assert.Multiple(
            () => Assert.True(result.Forecast.HasFlag(ForecastParams.Temperature)),
            () => Assert.True(result.Forecast.HasFlag(ForecastParams.WeatherCode)),
            () => Assert.True(result.AirQuality.HasFlag(AirQualityParams.Pm25)),
            () => Assert.True(result.AirQuality.HasFlag(AirQualityParams.Pm10)),
            () => Assert.False(result.Forecast.HasFlag(ForecastParams.WindSpeed)));
    }

    [Fact]
    public void Should_return_empty_requirements_when_no_rules_are_enabled()
    {
        var factory = new DomainRequirementsFactory(
        [
            new StubRule(false, new(Forecast: ForecastParams.Temperature)),
        ]);

        var result = factory.Create(string.Empty);

        Assert.Equal(new DomainRequirements(), result);
    }
}
