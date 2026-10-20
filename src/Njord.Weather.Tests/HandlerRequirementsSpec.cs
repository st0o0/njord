using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;
using Njord.Tests.Shared;
using Njord.Weather.Alerts;

namespace Njord.Weather.Tests;

public sealed class HandlerRequirementsSpec
{
    private static readonly Dictionary<FeatureKind, IDomainHandler> Handlers = Build();

    private static Dictionary<FeatureKind, IDomainHandler> Build()
    {
        using var provider = FeatureServices.Build(new FakeTimeProvider(), services => services
            .AddAlertOptions(AppSettingsDefaults.Configuration())
            .AddWeatherDomain());
        return provider.GetServices<IDomainHandler>().Where(h => h is not WeatherAlertHandler).ToDictionary(h => h.Feature.Kind);
    }

    [Theory]
    [InlineData(FeatureKind.CommuteSafety)]
    [InlineData(FeatureKind.ApproachingFront)]
    [InlineData(FeatureKind.BBQScore)]
    [InlineData(FeatureKind.ConfidenceTrend)]
    [InlineData(FeatureKind.GardeningAdvisory)]
    [InlineData(FeatureKind.LaundryScore)]
    [InlineData(FeatureKind.NightVentilation)]
    [InlineData(FeatureKind.SolarYield)]
    public void Should_require_only_the_weather_domain(FeatureKind kind)
    {
        var requirements = Handlers[kind].Feature.Requirements;

        Assert.Multiple(
            () => Assert.NotEqual(WeatherParams.None, requirements.Weather),
            () => Assert.Equal(AirQualityParams.None, requirements.AirQuality),
            () => Assert.False(requirements.Requires(DomainKind.AirQuality)),
            () => Assert.False(requirements.Requires(DomainKind.Flood)),
            () => Assert.False(requirements.Requires(DomainKind.Marine)));
    }

    [Theory]
    [InlineData(FeatureKind.ExerciseAdvisory)]
    [InlineData(FeatureKind.OutdoorScore)]
    [InlineData(FeatureKind.VentilationWindow)]
    public void Should_require_weather_and_air_quality(FeatureKind kind)
    {
        var requirements = Handlers[kind].Feature.Requirements;

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
            Handlers[FeatureKind.ConfidenceTrend].Feature.Requirements);
    }

    [Fact]
    public void Should_declare_the_radiation_params_the_solar_yield_reads()
    {
        Assert.Equal(
            new DomainRequirements(Weather: WeatherParams.GlobalTiltedIrradiance),
            Handlers[FeatureKind.SolarYield].Feature.Requirements);
    }

    [Fact]
    public void Should_declare_the_pm25_param_for_the_exercise_advisory()
    {
        var requirements = Handlers[FeatureKind.ExerciseAdvisory].Feature.Requirements;

        Assert.Equal(AirQualityParams.Pm25 | AirQualityParams.Ozone, requirements.AirQuality);
    }
}
