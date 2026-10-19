using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class TropicalNightRuleSpec
{
    private static readonly TropicalNightRule Rule = new();

    [Theory]
    [InlineData(21.0, AlertSeverity.Yellow, 20.0)]
    [InlineData(24.0, AlertSeverity.Orange, 23.0)]
    [InlineData(26.0, AlertSeverity.Red, 25.0)]
    [InlineData(20.0, AlertSeverity.None, 20.0)]
    public void Tropical_night_classifies_the_night_minimum_strictly(double night, AlertSeverity severity, double threshold)
    {
        var alert = Rule.Evaluate(
            Day((h, p) => h < 10 ? p with { Temperature = night, IsDay = false } : p with { Temperature = 35.0, IsDay = true }),
            Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(threshold, alert.Threshold),
            () => Assert.Equal(night, alert.TriggerValue),
            () => Assert.Equal(0, alert.HoursUntil),
            () => Assert.Equal(new TropicalNightDetails(night, severity == AlertSeverity.None ? 0 : 10), alert.Details));
    }

    [Fact]
    public void Tropical_night_uses_the_coldest_night_hour()
    {
        var alert = Rule.Evaluate(
            Day((h, p) => p with { Temperature = h == 3 ? 21.0 : 25.0, IsDay = false }), Defaults, Now);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(new TropicalNightDetails(21.0, 24), alert.Details);
    }

    [Fact]
    public void Tropical_night_without_night_hours_returns_the_none_alert()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = 28.0, IsDay = true }), Defaults, Now);

        Assert.Equal(Alert.None(AlertType.TropicalNight), alert);
    }

    [Fact]
    public void Tropical_night_unknown_daylight_returns_the_none_alert()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = 28.0 }), Defaults, Now);

        Assert.Equal(Alert.None(AlertType.TropicalNight), alert);
    }

    [Fact]
    public void Tropical_night_takes_confidence_from_the_coldest_night_point()
    {
        var forecast = Day((h, p) => p with { Temperature = h == 3 ? 21.0 : 25.0, IsDay = false, EnsembleStdDev = h == 3 ? 0.5 : 5.0 });

        Assert.Equal(ConfidenceLevel.High, Rule.Evaluate(forecast, Defaults, Now).Level);
    }

    [Fact]
    public void Tropical_night_without_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.TropicalNight), Rule.Evaluate(Empty(), Defaults, Now));
    }
}
