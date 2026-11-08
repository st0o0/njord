using Microsoft.Extensions.Options;
using Njord.Features.Alerts.Ice;
using Njord.Messages.Alerts;
using Njord.Messages.Results.Alerts;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.Ice;

public sealed class IceAlertTests
{
    private static IceAlert Rule() => new(Options.Create(new IceConfig { Enabled = true }));

    [Fact]
    public void Freezing_rain_weather_code_produces_red_with_soil_frozen()
    {
        var weather = Day((_, p) => p with { Temperature = -1.0, WeatherCode = 66, Rain = 1.0, SoilTemperature = -0.5 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<IceAlertResult>(result);
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.True(alert.SoilFrozen));
    }

    [Fact]
    public void Freezing_drizzle_weather_code_produces_orange()
    {
        var weather = Day((_, p) => p with { Temperature = -0.5, WeatherCode = 56, Rain = 0.5 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<IceAlertResult>(result);
        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Rain_at_freezing_point_produces_red()
    {
        var weather = Day((_, p) => p with { Temperature = 0.0, Rain = 1.0, WeatherCode = 0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<IceAlertResult>(result);
        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Rain_near_freezing_within_threshold_produces_yellow()
    {
        var weather = Day((_, p) => p with { Temperature = 1.5, Rain = 1.0, WeatherCode = 0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<IceAlertResult>(result);
        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void Warm_conditions_produce_null()
    {
        var weather = Day((_, p) => p with { Temperature = 10.0, Rain = 1.0, WeatherCode = 0 });
        Assert.Null(Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay())));
    }
}
