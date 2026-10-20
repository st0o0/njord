using Njord.Forecast.Flood;
using Njord.Messages.Alerts;
using Njord.Tests.Shared;
using static Njord.Tests.Shared.FloodFixtures;

namespace Njord.Alerts.Tests;

public sealed class FloodRainAlertRuleSpec
{
    private static readonly FloodRainAlertRule Rule = new();

    private static Alert Evaluate(double hourlyRain, params FloodPoint[] points) =>
        Rule.Evaluate(new FloodRainInput(FloodForecast(points), Rain(hourlyRain)), AlertRuleFixture.Defaults, Now);

    [Fact]
    public void Heavy_rain_with_rising_discharge_above_the_median_is_orange()
    {
        var alert = Evaluate(1.5, Point(0, 110), Point(1, 130));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(130.0, alert.TriggerValue),
            () => Assert.Equal(100.0, alert.Threshold));
    }

    [Fact]
    public void Heavy_rain_with_rising_discharge_above_p75_is_red()
    {
        var alert = Evaluate(1.5, Point(0, 110), Point(1, 170));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(150.0, alert.Threshold));
    }

    [Fact]
    public void Rain_exactly_at_30_mm_in_24_hours_counts_as_heavy()
    {
        Assert.Equal(AlertSeverity.Orange, Evaluate(1.25, Point(0, 110), Point(1, 130)).Severity);
    }

    [Fact]
    public void Rain_below_30_mm_in_24_hours_raises_nothing()
    {
        Assert.Equal(AlertSeverity.None, Evaluate(1.2, Point(0, 110), Point(1, 170)).Severity);
    }

    [Fact]
    public void Falling_discharge_raises_nothing()
    {
        Assert.Equal(AlertSeverity.None, Evaluate(1.5, Point(0, 170), Point(1, 120)).Severity);
    }

    [Fact]
    public void Discharge_below_the_median_raises_nothing()
    {
        Assert.Equal(AlertSeverity.None, Evaluate(1.5, Point(0, 50), Point(1, 90)).Severity);
    }
}
