using Njord.Messages.Alerts;
using Njord.Tests.Shared;
using static Njord.Marine.Tests.MarineFixtures;

namespace Njord.Marine.Tests;

public sealed class SwimmingConditionsRuleSpec
{
    private static readonly SwimmingConditionsRule Rule = new();

    [Theory]
    [InlineData(0.3, 20.0, AlertSeverity.None)]
    [InlineData(0.99, 16.0, AlertSeverity.None)]
    [InlineData(1.0, 20.0, AlertSeverity.Yellow)]
    [InlineData(0.3, 15.9, AlertSeverity.Yellow)]
    [InlineData(1.9, 20.0, AlertSeverity.Yellow)]
    [InlineData(2.0, 20.0, AlertSeverity.Red)]
    [InlineData(0.3, 9.9, AlertSeverity.Red)]
    [InlineData(1.2, 9.0, AlertSeverity.Red)]
    public void Wave_height_and_water_temperature_decide_the_severity(double waveHeight, double temperature, AlertSeverity expected)
    {
        var alert = Rule.Evaluate(MarineForecast(Point(0, waveHeight, temperature)), AlertRuleFixture.Defaults, Now);

        Assert.Equal(expected, alert.Severity);
    }

    [Fact]
    public void Missing_water_temperature_is_judged_by_wave_height_alone()
    {
        var calm = Rule.Evaluate(MarineForecast(Point(0, 0.3, null)), AlertRuleFixture.Defaults, Now);
        var rough = Rule.Evaluate(MarineForecast(Point(0, 1.5, null)), AlertRuleFixture.Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.None, calm.Severity),
            () => Assert.Equal(AlertSeverity.Yellow, rough.Severity));
    }

    [Fact]
    public void The_worst_hour_of_the_next_day_is_reported_with_its_lead_time()
    {
        var forecast = MarineForecast(Point(0, 0.4), Point(3, 1.4), Point(6, 2.6), Point(9, 2.2));

        var alert = Rule.Evaluate(forecast, AlertRuleFixture.Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.SwimmingConditions, alert.Type),
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(2.6, alert.TriggerValue),
            () => Assert.Equal(2.0, alert.Threshold),
            () => Assert.Equal(6, alert.HoursUntil));
    }

    [Fact]
    public void Hours_beyond_the_24_hour_window_are_ignored()
    {
        var alert = Rule.Evaluate(MarineForecast(Point(0, 0.4), Point(30, 3.0)), AlertRuleFixture.Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
