using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;
using Njord.Tests.Shared;

namespace Njord.IntegrationTests;

public sealed class HandlerRequirementsTests
{
    private static readonly Dictionary<string, IFeatureRule> Features =
        FeatureServices.AllFeatureRules().ToDictionary(f => f.GetType().Name, StringComparer.Ordinal);

    [Theory]
    [InlineData("CommuteSafety")]
    [InlineData("ApproachingFront")]
    [InlineData("BbqScore")]
    [InlineData("ConfidenceTrend")]
    [InlineData("Gardening")]
    [InlineData("LaundryScore")]
    [InlineData("NightVentilation")]
    [InlineData("SolarYield")]
    public void Should_require_only_the_weather_domain(string name)
    {
        var requirements = Features[name].Descriptor.Requirements;

        Assert.Multiple(
            () => Assert.NotEqual(WeatherParams.None, requirements.Weather),
            () => Assert.Equal(AirQualityParams.None, requirements.AirQuality),
            () => Assert.False(requirements.Requires(DomainKind.AirQuality)),
            () => Assert.False(requirements.Requires(DomainKind.Flood)),
            () => Assert.False(requirements.Requires(DomainKind.Marine)));
    }

    [Theory]
    [InlineData("ExerciseAdvisory")]
    [InlineData("OutdoorScore")]
    [InlineData("VentilationWindow")]
    public void Should_require_weather_and_air_quality(string name)
    {
        var requirements = Features[name].Descriptor.Requirements;

        Assert.Multiple(
            () => Assert.True(requirements.Requires(DomainKind.Weather)),
            () => Assert.True(requirements.Requires(DomainKind.AirQuality)),
            () => Assert.False(requirements.Requires(DomainKind.Flood)));
    }

    [Fact]
    public void Should_declare_only_the_params_the_confidence_trend_reads()
    {
        Assert.Equal(
            new DomainRequirements(Weather: WeatherParams.EnsembleStdDev),
            Features["ConfidenceTrend"].Descriptor.Requirements);
    }

    [Fact]
    public void Should_declare_the_radiation_params_the_solar_yield_reads()
    {
        Assert.Equal(
            new DomainRequirements(Weather: WeatherParams.GlobalTiltedIrradiance),
            Features["SolarYield"].Descriptor.Requirements);
    }

    [Fact]
    public void Should_declare_the_pm25_param_for_the_exercise_advisory()
    {
        var requirements = Features["ExerciseAdvisory"].Descriptor.Requirements;

        Assert.Equal(AirQualityParams.Pm25 | AirQualityParams.Ozone, requirements.AirQuality);
    }
}
