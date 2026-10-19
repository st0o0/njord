using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Alerts.Tests.Rules;

public sealed class PressureDropRuleSpec
{
    private static readonly PressureDropRule Rule = new();

    [Theory]
    [InlineData(1006.0, AlertSeverity.Yellow, 5.0, 7.0)]
    [InlineData(1002.0, AlertSeverity.Orange, 10.0, 11.0)]
    [InlineData(1010.0, AlertSeverity.None, 5.0, 3.0)]
    public void Pressure_drop_classifies_the_largest_drop_over_three_hours(
        double lowPressure, AlertSeverity severity, double threshold, double drop)
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { Pressure = h < 3 ? 1013.0 : lowPressure }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(threshold, alert.Threshold),
            () => Assert.Equal(drop, alert.TriggerValue),
            () => Assert.Equal(new PressureDropDetails(drop, 0), alert.Details));
    }

    [Fact]
    public void Pressure_drop_over_less_than_three_hours_is_ignored()
    {
        var pressures = new[] { 1013.0, 1013.0, 1005.0, 1013.0 };

        var alert = Rule.Evaluate(Hourly(4, (h, p) => p with { Pressure = pressures[h] }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(0.0, alert.TriggerValue);
    }

    [Fact]
    public void Pressure_rise_produces_none()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { Pressure = 1000.0 + h }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(0.0, alert.TriggerValue);
    }

    [Fact]
    public void Pressure_drop_with_fewer_than_four_points_returns_the_none_alert()
    {
        var alert = Rule.Evaluate(Hourly(3, (h, p) => p with { Pressure = 1013.0 - h * 10 }), Defaults, Now);

        Assert.Equal(Alert.None(AlertType.PressureDrop), alert);
    }

    [Fact]
    public void Pressure_drop_takes_confidence_from_the_end_of_the_drop()
    {
        var forecast = Day((h, p) => p with { Pressure = h < 3 ? 1013.0 : 1000.0, EnsembleStdDev = h == 3 ? 4.0 : 0.1 });

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Equal(ConfidenceLevel.Low, alert.Level);
    }

    [Fact]
    public void Pressure_drop_without_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.PressureDrop), Rule.Evaluate(Empty(), Defaults, Now));
    }
}
