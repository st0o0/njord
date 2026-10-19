using Njord.Flood;
using Njord.Messages.Alerts;
using Njord.Tests.Shared;

using static Njord.Flood.Tests.FloodFixtures;

namespace Njord.Flood.Tests;

public sealed class FloodRainAlertSpec
{
    private static readonly FloodRainAlertRule Rule = new();

    [Fact]
    public void Heavy_rain_with_rising_discharge_above_the_median_is_orange()
    {
        var flood = FloodForecast(Point(0, 110), Point(1, 130));

        var alert = Rule.Evaluate(new FloodRainInput(Lucerne, flood, Rain(1.5)), AlertRuleFixture.Defaults, Now);

        Assert.Equal(UnifiedAlert.Of(AlertType.FloodRain, AlertSeverity.Orange, 130.0, 100.0, 12), alert);
    }

    [Fact]
    public void Rising_discharge_above_p75_is_red()
    {
        var flood = FloodForecast(Point(0, 110), Point(1, 170));

        var alert = Rule.Evaluate(new FloodRainInput(Lucerne, flood, Rain(1.5)), AlertRuleFixture.Defaults, Now);

        Assert.Equal(UnifiedAlert.Of(AlertType.FloodRain, AlertSeverity.Red, 170.0, 150.0, 12), alert);
    }

    [Fact]
    public void Light_rain_raises_nothing()
    {
        Assert.Equal(AlertSeverity.None, Rule.Evaluate(new FloodRainInput(Lucerne, FloodForecast(Point(0, 110), Point(1, 170)), Rain(0.5)), AlertRuleFixture.Defaults, Now).Severity);
    }

    [Fact]
    public void Falling_discharge_raises_nothing()
    {
        Assert.Equal(AlertSeverity.None, Rule.Evaluate(new FloodRainInput(Lucerne, FloodForecast(Point(0, 170), Point(1, 120)), Rain(1.5)), AlertRuleFixture.Defaults, Now).Severity);
    }

    [Fact]
    public void Discharge_below_the_median_raises_nothing()
    {
        Assert.Equal(AlertSeverity.None, Rule.Evaluate(new FloodRainInput(Lucerne, FloodForecast(Point(0, 50), Point(1, 90)), Rain(1.5)), AlertRuleFixture.Defaults, Now).Severity);
    }
}
