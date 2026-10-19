using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class UvRuleSpec
{
    private static readonly UvRule Rule = new();

    [Theory]
    [InlineData(2.9, UvLevel.Low, AlertSeverity.None, 0.0)]
    [InlineData(3.0, UvLevel.Moderate, AlertSeverity.Yellow, 3.0)]
    [InlineData(5.9, UvLevel.Moderate, AlertSeverity.Yellow, 3.0)]
    [InlineData(6.0, UvLevel.High, AlertSeverity.Orange, 6.0)]
    [InlineData(7.9, UvLevel.High, AlertSeverity.Orange, 6.0)]
    [InlineData(8.0, UvLevel.VeryHigh, AlertSeverity.Red, 8.0)]
    [InlineData(10.9, UvLevel.VeryHigh, AlertSeverity.Red, 8.0)]
    [InlineData(11.0, UvLevel.Extreme, AlertSeverity.Red, 11.0)]
    public void Uv_level_boundaries_are_inclusive(double uv, UvLevel level, AlertSeverity severity, double threshold)
    {
        var alert = Rule.Evaluate(Uniform(p => p with { UvIndex = uv }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(threshold, alert.Threshold),
            () => Assert.Equal(uv, alert.TriggerValue),
            () => Assert.Equal(new UvDetails(level, uv), alert.Details));
    }

    [Fact]
    public void Uv_classifies_the_hourly_maximum_not_the_minimum()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { UvIndex = h == 13 ? 7.0 : 2.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(13, alert.HoursUntil),
            () => Assert.Equal(new UvDetails(UvLevel.High, 7.0), alert.Details));
    }

    [Fact]
    public void Uv_zero_everywhere_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.Uv), Rule.Evaluate(Day(), Defaults, Now));
    }

    [Fact]
    public void Uv_takes_confidence_from_the_peak_point()
    {
        var forecast = Day((h, p) => p with { UvIndex = h == 4 ? 9.0 : 1.0, EnsembleStdDev = h == 4 ? 4.0 : 0.1 });

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Equal(ConfidenceLevel.Low, alert.Level);
        Assert.Equal(0.25, alert.Confidence);
    }

    [Fact]
    public void Uv_without_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.Uv), Rule.Evaluate(Empty(), Defaults, Now));
    }
}
