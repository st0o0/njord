using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class WindChillRuleSpec
{
    private static readonly WindChillRule Rule = new();

    [Theory]
    [InlineData(-12.0, AlertSeverity.Yellow, -10.0, null)]
    [InlineData(-22.0, AlertSeverity.Orange, -20.0, ExposureRisk.Frostbite30Min)]
    [InlineData(-32.0, AlertSeverity.Red, -30.0, ExposureRisk.Frostbite10Min)]
    [InlineData(15.0, AlertSeverity.None, -10.0, null)]
    public void Wind_chill_classifies_the_lowest_felt_temperature(
        double felt, AlertSeverity severity, double threshold, ExposureRisk? risk)
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = felt + 7.0, ApparentTemperature = felt }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(threshold, alert.Threshold),
            () => Assert.Equal(felt, alert.TriggerValue),
            () => Assert.Equal(new WindChillDetails(felt, 7.0, risk), alert.Details));
    }

    [Fact]
    public void Wind_chill_reports_hours_until_the_coldest_hour()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { ApparentTemperature = h == 9 ? -15.0 : 0.0 }), Defaults, Now);

        Assert.Equal(9, alert.HoursUntil);
    }

    [Fact]
    public void Wind_chill_takes_confidence_from_the_coldest_point()
    {
        var forecast = Day((h, p) => p with { ApparentTemperature = h == 9 ? -15.0 : 0.0, EnsembleStdDev = h == 9 ? 3.5 : 0.1 });

        Assert.Equal(ConfidenceLevel.Low, Rule.Evaluate(forecast, Defaults, Now).Level);
    }

    [Fact]
    public void Wind_chill_without_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.WindChill), Rule.Evaluate(Empty(), Defaults, Now));
    }
}
