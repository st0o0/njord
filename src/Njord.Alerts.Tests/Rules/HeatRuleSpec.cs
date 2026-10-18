using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class HeatRuleSpec
{
    private static readonly HeatRule Rule = new();

    [Fact]
    public void Heat_extreme()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (ApparentTemp, 42.0)),
            MakeForecast(new WeatherModel("m2"), (ApparentTemp, 41.0)),
            MakeForecast(new WeatherModel("m3"), (ApparentTemp, 28.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.True(alert.Confidence > 0.5);
    }

    [Fact]
    public void Heat_moderate()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (ApparentTemp, 32.0)),
            MakeForecast(new WeatherModel("m2"), (ApparentTemp, 31.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void Heat_below_threshold_returns_none_alert()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (ApparentTemp, 28.0), (Temperature, 27.0)),
            MakeForecast(new WeatherModel("m2"), (ApparentTemp, 27.0), (Temperature, 26.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(0.0, alert.TriggerValue);
        Assert.Equal(0.0, alert.Threshold);
        Assert.Same(NoDetails.Instance, alert.Details);
    }

    [Fact]
    public void Heat_fires_when_raw_temp_exceeds_threshold_but_apparent_does_not()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (ApparentTemp, 28.0), (Temperature, 32.0)),
            MakeForecast(new WeatherModel("m2"), (ApparentTemp, 27.0), (Temperature, 31.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(31.5, alert.TriggerValue);
    }
}
