using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class HumidityRuleSpec
{
    private static readonly HumidityRule Rule = new();

    [Theory]
    [InlineData(18.0, AlertSeverity.Yellow, 16.0, HumidityComfort.Muggy)]
    [InlineData(22.0, AlertSeverity.Orange, 21.0, HumidityComfort.Oppressive)]
    [InlineData(25.0, AlertSeverity.Red, 24.0, HumidityComfort.Tropical)]
    public void Humidity_classifies_the_daytime_dew_point(
        double dewPoint, AlertSeverity severity, double threshold, HumidityComfort comfort)
    {
        var alert = Rule.Evaluate(Uniform(p => p with { DewPoint = dewPoint, IsDay = true }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(threshold, alert.Threshold),
            () => Assert.Equal(dewPoint, alert.TriggerValue),
            () => Assert.Equal(new HumidityDetails(dewPoint, comfort), alert.Details));
    }

    [Fact]
    public void Humidity_comfortable_produces_none_without_comfort_level()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { DewPoint = 10.0, IsDay = true }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.None, alert.Severity),
            () => Assert.Equal(new HumidityDetails(10.0, null), alert.Details));
    }

    [Fact]
    public void Humidity_ignores_nighttime_hours()
    {
        var forecast = Day((h, p) => p with { DewPoint = h < 12 ? 26.0 : 12.0, IsDay = h >= 12 });

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(12.0, alert.TriggerValue);
    }

    [Fact]
    public void Humidity_all_nighttime_returns_the_none_alert()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { DewPoint = 25.0, IsDay = false }), Defaults, Now);

        Assert.Equal(Alert.None(AlertType.Humidity), alert);
    }

    [Fact]
    public void Humidity_unknown_daylight_returns_the_none_alert()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { DewPoint = 25.0 }), Defaults, Now);

        Assert.Equal(Alert.None(AlertType.Humidity), alert);
    }

    [Fact]
    public void Humidity_reports_hours_until_the_muggiest_hour()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { DewPoint = h == 8 ? 19.0 : 10.0, IsDay = true }), Defaults, Now);

        Assert.Equal(8, alert.HoursUntil);
    }

    [Fact]
    public void Humidity_takes_confidence_from_the_muggiest_point()
    {
        var forecast = Day((h, p) => p with { DewPoint = h == 8 ? 19.0 : 10.0, IsDay = true, EnsembleStdDev = h == 8 ? 2.0 : 0.1 });

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Equal(ConfidenceLevel.Medium, alert.Level);
    }
}
