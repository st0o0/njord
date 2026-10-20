using Njord.Messages.Alerts;
using Njord.Tests.Shared;
using static Njord.AirQuality.Tests.AirQualityFixture;

namespace Njord.AirQuality.Tests;

public sealed class PollenAlertRuleSpec
{
    private static readonly PollenAlertRule Rule = new();

    [Fact]
    public void Pollen_at_the_yellow_threshold_is_raised()
    {
        var alert = Rule.Evaluate(Day((_, p) => p with { BirchPollen = 20.0 }), AlertRuleFixture.Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(20.0, alert.Threshold));
    }

    [Fact]
    public void Pollen_below_the_yellow_threshold_raises_nothing()
    {
        var alert = Rule.Evaluate(Day((_, p) => p with { BirchPollen = 19.9 }), AlertRuleFixture.Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Pollen_reports_the_highest_value_across_all_types()
    {
        var alert = Rule.Evaluate(
            Day((h, p) => p with { BirchPollen = h == 3 ? 30.0 : 1.0, GrassPollen = h == 9 ? 120.0 : 2.0 }), AlertRuleFixture.Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(120.0, alert.TriggerValue),
            () => Assert.Equal(9, alert.HoursUntil));
    }
}
