using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Alerts.Tests.Rules;

public sealed class VisibilityRuleSpec
{
    private static readonly VisibilityRule Rule = new();

    [Theory]
    [InlineData(800.0, AlertSeverity.Yellow, 1000.0)]
    [InlineData(150.0, AlertSeverity.Orange, 200.0)]
    [InlineData(40.0, AlertSeverity.Red, 50.0)]
    [InlineData(1000.0, AlertSeverity.None, 1000.0)]
    [InlineData(10000.0, AlertSeverity.None, 1000.0)]
    public void Visibility_classifies_the_minimum_strictly(double metres, AlertSeverity severity, double threshold)
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Visibility = metres }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(threshold, alert.Threshold),
            () => Assert.Equal(metres, alert.TriggerValue));
    }

    [Fact]
    public void Visibility_counts_hours_below_one_kilometre()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { Visibility = h is >= 3 and < 8 ? 500.0 : 9000.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(3, alert.HoursUntil),
            () => Assert.Equal(new VisibilityDetails(500.0, 5), alert.Details));
    }

    [Fact]
    public void Visibility_takes_confidence_from_the_lowest_point()
    {
        var forecast = Day((h, p) => p with { Visibility = h == 6 ? 100.0 : 9000.0, EnsembleStdDev = h == 6 ? 0.4 : 5.0 });

        Assert.Equal(ConfidenceLevel.High, Rule.Evaluate(forecast, Defaults, Now).Level);
    }

    [Fact]
    public void Visibility_without_visibility_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.Visibility), Rule.Evaluate(Day(), Defaults, Now));
    }

    [Fact]
    public void Visibility_without_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.Visibility), Rule.Evaluate(Empty(), Defaults, Now));
    }
}
