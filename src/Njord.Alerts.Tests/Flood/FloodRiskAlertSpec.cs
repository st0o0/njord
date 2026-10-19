using System.Collections.Immutable;
using Microsoft.Extensions.Time.Testing;
using Njord.Alerts.Flood;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;

namespace Njord.Alerts.Tests.Flood;

using static Njord.Alerts.Tests.Flood.FloodFixtures;

public sealed class FloodRiskAlertSpec
{
    private static readonly FloodRiskAlertRule Rule = new();

    [Fact]
    public void Discharge_within_normal_range_returns_the_none_alert()
    {
        Assert.Equal(FloodAlert.None(FloodAlertType.FloodRisk), Rule.Evaluate(Flood(Point(0, 190, median: 100, p75: 300)), Now));
    }

    [Fact]
    public void Discharge_above_twice_the_median_but_below_p75_is_yellow()
    {
        var alert = Rule.Evaluate(Flood(Point(0, 210, median: 100, p75: 300)), Now);

        Assert.Equal(new FloodAlert(FloodAlertType.FloodRisk, AlertSeverity.Yellow, 210.0, 200.0, 0), alert);
    }

    [Fact]
    public void Discharge_above_p75_is_orange_with_the_p75_threshold()
    {
        var alert = Rule.Evaluate(Flood(Point(0, 160, median: 100, p75: 150)), Now);

        Assert.Equal(new FloodAlert(FloodAlertType.FloodRisk, AlertSeverity.Orange, 160.0, 150.0, 0), alert);
    }

    [Fact]
    public void Discharge_above_the_historical_max_is_red()
    {
        var alert = Rule.Evaluate(Flood(Point(2, 450, max: 400)), Now);

        Assert.Equal(new FloodAlert(FloodAlertType.FloodRisk, AlertSeverity.Red, 450.0, 400.0, 36), alert);
    }

    [Fact]
    public void The_highest_severity_in_the_next_seven_days_wins()
    {
        var alert = Rule.Evaluate(Flood(Point(0, 160), Point(3, 500), Point(5, 170)), Now);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.Equal(500.0, alert.TriggerValue);
    }

    [Fact]
    public void Days_beyond_the_window_are_ignored()
    {
        var alert = Rule.Evaluate(Flood(Point(10, 900)), Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Missing_percentiles_never_raise()
    {
        var forecast = new DomainForecast<FloodPoint>(
            DomainKind.Flood, Now, TimeResolution.Daily, [new FloodPoint(Today, 9999)]);

        Assert.Equal(AlertSeverity.None, Rule.Evaluate(forecast, Now).Severity);
    }

    [Fact]
    public void An_empty_forecast_returns_the_none_alert()
    {
        Assert.Equal(FloodAlert.None(FloodAlertType.FloodRisk), Rule.Evaluate(Flood(), Now));
    }
}
