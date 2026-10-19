using Njord.Alerts.AirQuality;
using Njord.Messages.Alerts;
using static Njord.Alerts.Tests.AirQuality.AirQualityFixture;

namespace Njord.Alerts.Tests.AirQuality;

public sealed class Pm25AlertRuleSpec
{
    private static readonly Pm25AlertRule Rule = new();

    [Theory]
    [InlineData(14.9, AlertSeverity.None, 0.0)]
    [InlineData(15.0, AlertSeverity.Yellow, 15.0)]
    [InlineData(24.9, AlertSeverity.Yellow, 15.0)]
    [InlineData(25.0, AlertSeverity.Orange, 25.0)]
    [InlineData(50.0, AlertSeverity.Red, 50.0)]
    public void Severity_boundaries_follow_the_who_guideline(double value, AlertSeverity severity, double threshold)
    {
        var alert = Rule.Evaluate(Day((_, p) => p with { Pm25 = value }), Now);

        Assert.Equal(severity, alert.Severity);
        Assert.Equal(threshold, alert.Threshold);
    }

    [Fact]
    public void The_hourly_peak_and_its_offset_are_reported()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { Pm25 = h == 5 ? 31.04 : 8.0 }), Now);

        Assert.Equal(new AirQualityAlert(AirQualityAlertType.Pm25, AlertSeverity.Orange, 31.0, 25.0, 5), alert);
    }

    [Fact]
    public void An_empty_forecast_returns_the_none_alert()
    {
        var empty = Day() with { Points = [] };

        Assert.Equal(AirQualityAlert.None(AirQualityAlertType.Pm25), Rule.Evaluate(empty, Now));
    }
}
