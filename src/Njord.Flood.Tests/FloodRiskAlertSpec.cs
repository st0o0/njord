using Njord.Messages.Alerts;
using Njord.Model;
using Njord.Tests.Shared;

using static Njord.Flood.Tests.FloodFixtures;

namespace Njord.Flood.Tests;

public sealed class FloodRiskAlertSpec
{
    private static readonly FloodRiskAlertRule Rule = new();

    [Fact]
    public void Discharge_within_normal_range_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.FloodRisk), Rule.Evaluate(FloodForecast(Point(0, 190, median: 100, p75: 300)), AlertRuleFixture.Defaults, Now));
    }

    [Fact]
    public void Discharge_above_twice_the_median_but_below_p75_is_yellow()
    {
        var alert = Rule.Evaluate(FloodForecast(Point(0, 210, median: 100, p75: 300)), AlertRuleFixture.Defaults, Now);

        Assert.Equal(UnifiedAlert.Of(AlertType.FloodRisk, AlertSeverity.Yellow, 210.0, 200.0, 0), alert);
    }

    [Fact]
    public void Discharge_above_p75_is_orange_with_the_p75_threshold()
    {
        var alert = Rule.Evaluate(FloodForecast(Point(0, 160, median: 100, p75: 150)), AlertRuleFixture.Defaults, Now);

        Assert.Equal(UnifiedAlert.Of(AlertType.FloodRisk, AlertSeverity.Orange, 160.0, 150.0, 0), alert);
    }

    [Fact]
    public void Discharge_above_the_historical_max_is_red()
    {
        var alert = Rule.Evaluate(FloodForecast(Point(2, 450, max: 400)), AlertRuleFixture.Defaults, Now);

        Assert.Equal(UnifiedAlert.Of(AlertType.FloodRisk, AlertSeverity.Red, 450.0, 400.0, 36), alert);
    }

    [Fact]
    public void The_highest_severity_in_the_next_seven_days_wins()
    {
        var alert = Rule.Evaluate(FloodForecast(Point(0, 160), Point(3, 500), Point(5, 170)), AlertRuleFixture.Defaults, Now);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.Equal(500.0, alert.TriggerValue);
    }

    [Fact]
    public void Days_beyond_the_window_are_ignored()
    {
        var alert = Rule.Evaluate(FloodForecast(Point(10, 900)), AlertRuleFixture.Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Missing_percentiles_never_raise()
    {
        var forecast = new DomainForecast<FloodPoint>(
            DomainKind.Flood, Now, TimeResolution.Daily, [new FloodPoint(Today, 9999)]);

        Assert.Equal(AlertSeverity.None, Rule.Evaluate(forecast, AlertRuleFixture.Defaults, Now).Severity);
    }

    [Fact]
    public void An_empty_forecast_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.FloodRisk), Rule.Evaluate(FloodForecast(), AlertRuleFixture.Defaults, Now));
    }
}
