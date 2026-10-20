using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Njord.Advisories;
using Njord.Alerts;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Weather;
using Njord.Insights;
using Njord.Scores;
using Njord.Tests.Shared;

namespace Njord.IntegrationTests;

public sealed class HandlerRequirementsSpec
{
    private static readonly Dictionary<string, IDomainHandler> Handlers = Build();

    private static Dictionary<string, IDomainHandler> Build()
    {
        using var provider = FeatureServices.Build(new FakeTimeProvider(), services => services
            .AddAlertOptions(AppSettingsDefaults.Configuration())
            .AddAlertHandlers().AddScoreHandlers().AddInsightHandlers().AddAdvisoryHandlers());
        return provider.GetServices<IDomainHandler>().Where(h => h is not WeatherAlertHandler).ToDictionary(h => h.Name, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("Commute")]
    [InlineData("FrontApproaching")]
    [InlineData("Bbq")]
    [InlineData("ConfidenceTrend")]
    [InlineData("Gardening")]
    [InlineData("Laundry")]
    [InlineData("NightVentilation")]
    [InlineData("SolarYield")]
    public void Should_require_only_the_weather_domain(string name)
    {
        var requirements = Handlers[name].Feature.Requirements;

        Assert.Multiple(
            () => Assert.NotEqual(WeatherParams.None, requirements.Weather),
            () => Assert.Equal(AirQualityParams.None, requirements.AirQuality),
            () => Assert.False(requirements.Requires(DomainKind.AirQuality)),
            () => Assert.False(requirements.Requires(DomainKind.Flood)),
            () => Assert.False(requirements.Requires(DomainKind.Marine)));
    }

    [Theory]
    [InlineData("Exercise")]
    [InlineData("Outdoor")]
    [InlineData("Ventilation")]
    public void Should_require_weather_and_air_quality(string name)
    {
        var requirements = Handlers[name].Feature.Requirements;

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
            Handlers["ConfidenceTrend"].Feature.Requirements);
    }

    [Fact]
    public void Should_declare_the_radiation_params_the_solar_yield_reads()
    {
        Assert.Equal(
            new DomainRequirements(Weather: WeatherParams.GlobalTiltedIrradiance),
            Handlers["SolarYield"].Feature.Requirements);
    }

    [Fact]
    public void Should_declare_the_pm25_param_for_the_exercise_advisory()
    {
        var requirements = Handlers["Exercise"].Feature.Requirements;

        Assert.Equal(AirQualityParams.Pm25 | AirQualityParams.Ozone, requirements.AirQuality);
    }
}
