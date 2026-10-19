using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class HeatRuleSpec
{
    private static readonly HeatRule Rule = new();

    [Theory]
    [InlineData(31.0, AlertSeverity.Yellow, 30.0)]
    [InlineData(36.0, AlertSeverity.Orange, 35.0)]
    [InlineData(42.0, AlertSeverity.Red, 40.0)]
    public void Heat_classifies_the_apparent_maximum(double apparent, AlertSeverity severity, double threshold)
    {
        var alert = Rule.Evaluate(Uniform(p => p with { ApparentTemperature = apparent }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(threshold, alert.Threshold),
            () => Assert.Equal(apparent, alert.TriggerValue),
            () => Assert.Equal(new HeatDetails(apparent, 0), alert.Details));
    }

    [Fact]
    public void Heat_uses_the_air_temperature_when_it_exceeds_the_apparent_temperature()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = 36.0, ApparentTemperature = 30.0 }), Defaults, Now);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.Equal(36.0, alert.TriggerValue);
    }

    [Fact]
    public void Heat_reports_hours_until_the_peak()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { ApparentTemperature = h == 6 ? 41.0 : 20.0 }), Defaults, Now);

        Assert.Equal(6, alert.HoursUntil);
    }

    [Fact]
    public void Heat_below_advisory_returns_the_none_alert()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { ApparentTemperature = 25.0 }), Defaults, Now);

        Assert.Equal(Alert.None(AlertType.Heat), alert);
    }

    [Fact]
    public void Heat_takes_confidence_from_the_peak_point()
    {
        var forecast = Day((h, p) => p with { ApparentTemperature = h == 3 ? 38.0 : 20.0, EnsembleStdDev = h == 3 ? 4.5 : 0.1 });

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Equal(ConfidenceLevel.Low, alert.Level);
        Assert.Equal(0.25, alert.Confidence);
    }

    [Fact]
    public void Heat_without_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.Heat), Rule.Evaluate(Empty(), Defaults, Now));
    }
}
