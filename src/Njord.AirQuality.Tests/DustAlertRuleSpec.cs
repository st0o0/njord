using Njord.Messages.Alerts;
using Njord.Tests.Shared;
using static Njord.AirQuality.Tests.AirQualityFixture;

namespace Njord.AirQuality.Tests;

public sealed class DustAlertRuleSpec
{
    private static readonly DustAlertRule Rule = new();

    [Fact]
    public void Dust_at_the_first_threshold_is_yellow()
    {
        var alert = Rule.Evaluate(Day((_, p) => p with { Dust = 50.0 }), AlertRuleFixture.Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(50.0, alert.Threshold),
            () => Assert.Equal(50.0, alert.TriggerValue));
    }

    [Fact]
    public void Dust_below_the_first_threshold_raises_nothing()
    {
        var alert = Rule.Evaluate(Day((_, p) => p with { Dust = 49.9 }), AlertRuleFixture.Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Dust_at_the_orange_threshold_is_orange()
    {
        var alert = Rule.Evaluate(Day((_, p) => p with { Dust = 150.0 }), AlertRuleFixture.Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(150.0, alert.Threshold));
    }

    [Fact]
    public void Dust_at_the_red_threshold_is_red()
    {
        var alert = Rule.Evaluate(Day((_, p) => p with { Dust = 500.0 }), AlertRuleFixture.Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(500.0, alert.Threshold));
    }

    [Fact]
    public void Dust_reports_the_hourly_peak_and_its_offset()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { Dust = h == 6 ? 200.0 : 10.0 }), AlertRuleFixture.Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(200.0, alert.TriggerValue),
            () => Assert.Equal(150.0, alert.Threshold),
            () => Assert.Equal(6, alert.HoursUntil));
    }

    [Fact]
    public void No_dust_values_raises_nothing()
    {
        var alert = Rule.Evaluate(Day(), AlertRuleFixture.Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Feature_requires_dust_and_triggers_air_quality()
    {
        Assert.Multiple(
            () => Assert.Equal("Dust", Rule.Name),
            () => Assert.False(Rule.Feature.DefaultEnabled),
            () => Assert.Equal(Njord.Forecast.AirQuality.AirQualityParams.Dust, Rule.Feature.Requirements.AirQuality));
    }
}
