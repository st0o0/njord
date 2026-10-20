using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;
using Njord.Tests.Shared;
using Njord.Weather.Alerts;
using Njord.Weather.Lifestyle;

namespace Njord.Weather.Tests;

public sealed class HandlerRequirementsSpec
{
    private static readonly Dictionary<string, IDomainHandler> Handlers = Build();

    private static Dictionary<string, IDomainHandler> Build()
    {
        using var provider = FeatureServices.Build(new FakeTimeProvider(), services => services
            .AddAlertOptions(AppSettingsDefaults.Configuration())
            .AddWeatherDomain());
        return provider.GetServices<IDomainHandler>().ToDictionary(h => h.ConfigKey);
    }

    [Theory]
    [InlineData("WeatherAlerts")]
    [InlineData("CommuteSafety")]
    [InlineData("ApproachingFront")]
    [InlineData("BBQScore")]
    [InlineData("ConfidenceTrend")]
    [InlineData("GardeningAdvisory")]
    [InlineData("LaundryScore")]
    [InlineData("NightVentilation")]
    [InlineData("SolarYield")]
    public void Should_require_only_the_weather_domain(string configKey)
    {
        var requirements = Handlers[configKey].Requirements;

        Assert.Multiple(
            () => Assert.NotEqual(WeatherParam.None, requirements.Weather),
            () => Assert.Equal(AirQualityParam.None, requirements.AirQuality),
            () => Assert.False(requirements.Requires(DomainKind.AirQuality)),
            () => Assert.False(requirements.Requires(DomainKind.Flood)),
            () => Assert.False(requirements.Requires(DomainKind.Marine)));
    }

    [Theory]
    [InlineData("ExerciseAdvisory")]
    [InlineData("OutdoorScore")]
    [InlineData("VentilationWindow")]
    public void Should_require_weather_and_air_quality(string configKey)
    {
        var requirements = Handlers[configKey].Requirements;

        Assert.Multiple(
            () => Assert.True(requirements.Requires(DomainKind.Weather)),
            () => Assert.True(requirements.Requires(DomainKind.AirQuality)),
            () => Assert.False(requirements.Requires(DomainKind.Flood)));
    }

    [Fact]
    public void Should_declare_only_the_params_the_confidence_trend_reads()
    {
        Assert.Equal(
            new DomainRequirements(Weather: WeatherParam.EnsembleStdDev),
            Handlers["ConfidenceTrend"].Requirements);
    }

    [Fact]
    public void Should_declare_the_radiation_params_the_solar_yield_reads()
    {
        Assert.Equal(
            new DomainRequirements(Weather: WeatherParam.DirectRadiation | WeatherParam.DiffuseRadiation),
            Handlers["SolarYield"].Requirements);
    }

    [Fact]
    public void Should_declare_the_pm25_param_for_the_exercise_advisory()
    {
        var requirements = Handlers["ExerciseAdvisory"].Requirements;

        Assert.Equal(AirQualityParam.Pm25 | AirQualityParam.Ozone, requirements.AirQuality);
    }
}
