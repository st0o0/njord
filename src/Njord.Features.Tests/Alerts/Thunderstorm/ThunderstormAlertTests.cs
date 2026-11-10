using Microsoft.Extensions.Options;
using Njord.Core.Forecast;
using Njord.Features.Alerts.Thunderstorm;
using Njord.Messages.Alerts;
using Njord.Messages.Results.Alerts;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.Thunderstorm;

public sealed class ThunderstormAlertTests
{
    private static ThunderstormAlert Rule()
    {
        var c = new ThunderstormConfig { Enabled = true };
        c.ApplyDefaults();
        return new ThunderstormAlert(Options.Create(c));
    }

    [Fact]
    public void Extreme_cape_and_strong_gusts_produce_red_severity()
    {
        var weather = Day((_, p) => p with { Cape = 3000.0, WindGusts = 26.0, Precipitation = 0.0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<ThunderstormAlertResult>(result);
        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Moderate_cape_and_gusts_produce_orange_severity()
    {
        var weather = Day((_, p) => p with { Cape = 1600.0, WindGusts = 21.0, Precipitation = 0.0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<ThunderstormAlertResult>(result);
        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Low_cape_above_threshold_with_mild_gusts_produce_yellow_severity()
    {
        var weather = Day((_, p) => p with { Cape = 1100.0, WindGusts = 10.0, Precipitation = 0.0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<ThunderstormAlertResult>(result);
        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void High_cin_downgrades_severity()
    {
        var weather = Day((_, p) => p with { Cape = 3000.0, WindGusts = 26.0, Precipitation = 0.0, ConvectiveInhibition = 250.0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<ThunderstormAlertResult>(result);
        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Cape_below_threshold_produces_null()
    {
        var weather = Day((_, p) => p with { Cape = 500.0, WindGusts = 10.0 });
        Assert.Null(Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay())));
    }

    [Fact]
    public void Duration_hours_reflects_the_number_of_storm_hours()
    {
        var weather = Day((hour, p) => p with { Cape = hour < 6 ? 1200.0 : 500.0, WindGusts = 10.0, Precipitation = 0.0 });
        var result = Run(Rule(), Now, ForecastWithEnsemble(weather, EnsembleDay()));

        var alert = Assert.IsType<ThunderstormAlertResult>(result);
        Assert.Equal(6, alert.DurationHours);
    }

    [Theory]
    [InlineData(2600, 26.0, 0.0, AlertSeverity.Red)]
    [InlineData(2600, 0.0, 12.0, AlertSeverity.Red)]
    [InlineData(1600, 21.0, 0.0, AlertSeverity.Orange)]
    [InlineData(1600, 0.0, 6.0, AlertSeverity.Orange)]
    [InlineData(1100, 10.0, 0.0, AlertSeverity.Yellow)]
    [InlineData(900, 30.0, 20.0, AlertSeverity.Yellow)]
    public void HourSeverity_classifies_by_cape_gusts_and_precipitation(
        double cape, double gusts, double precip, AlertSeverity expected)
    {
        var cfg = new ThunderstormConfig { Enabled = true };
        cfg.ApplyDefaults();
        var point = new ForecastPoint(Now, Cape: cape, WindGusts: gusts, Precipitation: precip);
        Assert.Equal(expected, ThunderstormAlert.HourSeverity(point, cfg, cfg.CapeThresholds));
    }

    [Theory]
    [InlineData(AlertSeverity.Red, AlertSeverity.Orange)]
    [InlineData(AlertSeverity.Orange, AlertSeverity.Yellow)]
    [InlineData(AlertSeverity.Yellow, AlertSeverity.Yellow)]
    public void Downgrade_reduces_severity_by_one_step(AlertSeverity input, AlertSeverity expected)
    {
        Assert.Equal(expected, ThunderstormAlert.Downgrade(input));
    }
}
