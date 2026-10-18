using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class WindChillRuleSpec
{
    private static readonly WindChillRule Rule = new();

    [Fact]
    public void WindChill_moderate_produces_yellow()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (ApparentTemp, -12.0), (Temperature, -5.0)),
            MakeForecast(new WeatherModel("m2"), (ApparentTemp, -11.0), (Temperature, -4.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.True(Assert.IsType<WindChillDetails>(alert.Details).WindFactor > 5.0);
    }

    [Fact]
    public void WindChill_severe_produces_orange_with_frostbite_risk()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (ApparentTemp, -22.0), (Temperature, -10.0)),
            MakeForecast(new WeatherModel("m2"), (ApparentTemp, -21.0), (Temperature, -9.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.Equal(ExposureRisk.Frostbite30Min, Assert.IsType<WindChillDetails>(alert.Details).ExposureRisk);
    }

    [Fact]
    public void WindChill_extreme_produces_red()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (ApparentTemp, -35.0), (Temperature, -20.0)),
            MakeForecast(new WeatherModel("m2"), (ApparentTemp, -33.0), (Temperature, -18.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.Equal(ExposureRisk.Frostbite10Min, Assert.IsType<WindChillDetails>(alert.Details).ExposureRisk);
    }

    [Fact]
    public void WindChill_cold_but_not_extreme_produces_none()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (ApparentTemp, -5.0), (Temperature, -2.0)),
            MakeForecast(new WeatherModel("m2"), (ApparentTemp, -4.0), (Temperature, -1.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
