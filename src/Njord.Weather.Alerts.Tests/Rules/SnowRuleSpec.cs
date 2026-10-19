using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Alerts.Tests.Rules;

public sealed class SnowRuleSpec
{
    private static readonly SnowRule Rule = new();

    [Theory]
    [InlineData(0.1, AlertSeverity.Yellow, 0.0, 2.4)]
    [InlineData(0.5, AlertSeverity.Orange, 5.0, 12.0)]
    [InlineData(1.0, AlertSeverity.Red, 20.0, 24.0)]
    public void Snow_classifies_the_24h_total(double perHour, AlertSeverity severity, double threshold, double total)
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Snowfall = perHour }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(threshold, alert.Threshold),
            () => Assert.Equal(total, alert.TriggerValue),
            () => Assert.Equal(total, Assert.IsType<SnowDetails>(alert.Details).ExpectedAccumulation));
    }

    [Fact]
    public void Snow_reports_the_median_freezing_level()
    {
        var alert = Rule.Evaluate(
            Day((h, p) => p with { Snowfall = 0.5, FreezingLevelHeight = h % 2 == 0 ? 600.0 : 800.0 }), Defaults, Now);

        Assert.Equal(new SnowDetails(12.0, 700.0, 0), alert.Details);
    }

    [Fact]
    public void Snow_without_freezing_level_reports_none()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Snowfall = 0.5 }), Defaults, Now);

        Assert.Null(Assert.IsType<SnowDetails>(alert.Details).FreezingLevel);
    }

    [Fact]
    public void Snow_zero_snowfall_produces_none()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Snowfall = 0.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.None, alert.Severity),
            () => Assert.Equal(0.0, alert.TriggerValue));
    }

    [Fact]
    public void Snow_without_snowfall_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.Snow), Rule.Evaluate(Day(), Defaults, Now));
    }

    [Fact]
    public void Snow_takes_confidence_from_the_snowiest_point()
    {
        var forecast = Day((h, p) => p with { Snowfall = h == 4 ? 2.0 : 0.0, EnsembleStdDev = h == 4 ? 0.5 : 5.0 });

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Equal(ConfidenceLevel.High, alert.Level);
    }

    [Fact]
    public void Snow_without_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.Snow), Rule.Evaluate(Empty(), Defaults, Now));
    }
}
