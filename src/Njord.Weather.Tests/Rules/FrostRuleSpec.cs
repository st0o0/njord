using Njord.Messages.Alerts;
using Njord.Model;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class FrostRuleSpec
{
    private static readonly FrostRule Rule = new();

    [Theory]
    [InlineData(-2.0, AlertSeverity.Yellow, 0.0)]
    [InlineData(-8.0, AlertSeverity.Orange, -5.0)]
    [InlineData(-18.0, AlertSeverity.Red, -15.0)]
    [InlineData(5.0, AlertSeverity.None, 0.0)]
    public void Frost_classifies_the_24h_minimum(double temperature, AlertSeverity severity, double threshold)
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = temperature }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(threshold, alert.Threshold),
            () => Assert.Equal(temperature, alert.TriggerValue));
    }

    [Fact]
    public void Frost_reports_earliest_hour_and_low()
    {
        var forecast = Day((h, p) => p with { Temperature = h switch { 4 => -3.0, 9 => -6.0, _ => 10.0 } });

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(4, alert.HoursUntil),
            () => Assert.Equal(new FrostDetails(-6.0, Horizon.Hours(4), 0), alert.Details));
    }

    [Fact]
    public void Frost_without_frost_has_no_earliest_hour()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = 5.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Null(alert.HoursUntil),
            () => Assert.Equal(new FrostDetails(5.0, null, 0), alert.Details));
    }

    [Fact]
    public void Frost_rule_classifies_against_the_passed_thresholds()
    {
        var settings = Defaults with { Frost = new SeverityThresholds(0, -2, -5, ScanDirection.Min) };

        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = -3.0 }), settings, Now);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.Equal(-2.0, alert.Threshold);
    }

    [Fact]
    public void Frost_ignores_points_before_the_window_start()
    {
        var forecast = Hourly(30, (h, p) => p with { ValidFrom = p.ValidFrom.AddHours(-2), Temperature = h < 2 ? -20.0 : 8.0 });

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(8.0, alert.TriggerValue);
    }

    [Fact]
    public void Frost_floors_now_to_the_hour()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = -2.0 }), Defaults, Now.AddMinutes(40));

        Assert.Equal(0, alert.HoursUntil);
    }

    [Fact]
    public void Frost_small_ensemble_spread_gives_high_confidence()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = -2.0, EnsembleStdDev = 0.5 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(ConfidenceLevel.High, alert.Level),
            () => Assert.Equal(1.0, alert.Confidence));
    }

    [Fact]
    public void Frost_large_ensemble_spread_gives_low_confidence()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = -2.0, EnsembleStdDev = 4.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(ConfidenceLevel.Low, alert.Level),
            () => Assert.Equal(0.25, alert.Confidence));
    }

    [Fact]
    public void Frost_without_ensemble_spread_gives_medium_confidence()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = -2.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(ConfidenceLevel.Medium, alert.Level),
            () => Assert.Equal(0.5, alert.Confidence));
    }

    [Fact]
    public void Frost_takes_confidence_from_the_coldest_point()
    {
        var forecast = Day((h, p) => p with
        {
            Temperature = h == 7 ? -9.0 : -1.0,
            EnsembleStdDev = h == 7 ? 0.2 : 5.0,
        });

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Equal(ConfidenceLevel.High, alert.Level);
    }

    [Fact]
    public void Frost_without_data_returns_the_none_alert()
    {
        var alert = Rule.Evaluate(Empty(), Defaults, Now);

        Assert.Equal(Alert.None(AlertType.Frost), alert);
    }
}
