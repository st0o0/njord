using Njord.Messages.Alerts;
using static Njord.AirQuality.Tests.AirQualityFixture;
using Njord.Tests.Shared;

namespace Njord.AirQuality.Tests;

public sealed class Pm25AlertRuleSpec
{
    private static readonly Pm25AlertRule Rule = new();

    [Fact]
    public void Pm25_at_the_who_guideline_is_yellow()
    {
        var alert = Rule.Evaluate(Day((_, p) => p with { Pm25 = 15.0 }), AlertRuleFixture.Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(15.0, alert.Threshold),
            () => Assert.Equal(15.0, alert.TriggerValue));
    }

    [Fact]
    public void Pm25_below_the_who_guideline_raises_nothing()
    {
        var alert = Rule.Evaluate(Day((_, p) => p with { Pm25 = 14.9 }), AlertRuleFixture.Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Pm25_reports_the_hourly_peak_and_its_offset()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { Pm25 = h == 5 ? 31.04 : 8.0 }), AlertRuleFixture.Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(31.0, alert.TriggerValue),
            () => Assert.Equal(25.0, alert.Threshold),
            () => Assert.Equal(5, alert.HoursUntil));
    }
}
