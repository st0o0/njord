using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Alerts.Tests.Rules;

public sealed class StormRuleSpec
{
    private static readonly StormRule Rule = new();

    [Theory]
    [InlineData(20.0, AlertSeverity.Yellow, 17.0)]
    [InlineData(27.0, AlertSeverity.Orange, 25.0)]
    [InlineData(35.0, AlertSeverity.Red, 33.0)]
    [InlineData(10.0, AlertSeverity.None, 17.0)]
    public void Storm_classifies_the_peak_gust(double gust, AlertSeverity severity, double threshold)
    {
        var alert = Rule.Evaluate(Uniform(p => p with { WindGusts = gust }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(threshold, alert.Threshold),
            () => Assert.Equal(gust, alert.TriggerValue),
            () => Assert.Equal(new StormDetails(gust, 0), alert.Details));
    }

    [Fact]
    public void Storm_reports_hours_until_the_peak_gust()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { WindGusts = h == 5 ? 30.0 : 8.0 }), Defaults, Now);

        Assert.Equal(5, alert.HoursUntil);
    }

    [Fact]
    public void Storm_takes_confidence_from_the_peak_point()
    {
        var forecast = Day((h, p) => p with { WindGusts = h == 5 ? 30.0 : 8.0, EnsembleStdDev = h == 5 ? 3.5 : 0.1 });

        Assert.Equal(ConfidenceLevel.Low, Rule.Evaluate(forecast, Defaults, Now).Level);
    }

    [Fact]
    public void Storm_without_gust_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.Storm), Rule.Evaluate(Day(), Defaults, Now));
    }

    [Fact]
    public void Storm_without_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.Storm), Rule.Evaluate(Empty(), Defaults, Now));
    }
}
