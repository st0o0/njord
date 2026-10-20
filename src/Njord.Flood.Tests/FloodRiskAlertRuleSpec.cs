using Njord.Messages.Alerts;
using Njord.Tests.Shared;
using static Njord.Flood.Tests.FloodFixtures;

namespace Njord.Flood.Tests;

public sealed class FloodRiskAlertRuleSpec
{
    private static readonly FloodRiskAlertRule Rule = new();

    [Fact]
    public void Discharge_above_the_p75_percentile_is_orange()
    {
        var alert = Rule.Evaluate(FloodForecast(Point(0, 160, median: 100, p75: 150)), AlertRuleFixture.Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(160.0, alert.TriggerValue),
            () => Assert.Equal(150.0, alert.Threshold));
    }

    [Fact]
    public void Discharge_at_the_p75_percentile_raises_nothing()
    {
        var alert = Rule.Evaluate(FloodForecast(Point(0, 150, median: 100, p75: 150)), AlertRuleFixture.Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Discharge_within_twice_the_median_raises_nothing()
    {
        var alert = Rule.Evaluate(FloodForecast(Point(0, 190, median: 100, p75: 300)), AlertRuleFixture.Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Discharge_above_the_historical_maximum_is_red()
    {
        var alert = Rule.Evaluate(FloodForecast(Point(2, 450, max: 400)), AlertRuleFixture.Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(400.0, alert.Threshold),
            () => Assert.Equal(36, alert.HoursUntil));
    }

    [Fact]
    public void The_highest_severity_in_the_window_wins()
    {
        var alert = Rule.Evaluate(FloodForecast(Point(0, 160), Point(3, 500), Point(5, 170)), AlertRuleFixture.Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(500.0, alert.TriggerValue));
    }

    [Fact]
    public void Days_beyond_the_seven_day_window_are_ignored()
    {
        var alert = Rule.Evaluate(FloodForecast(Point(10, 900)), AlertRuleFixture.Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
