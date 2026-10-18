using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class StormRuleSpec
{
    private static readonly StormRule Rule = new();

    [Fact]
    public void Storm_detected()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (WindGusts, 20.0)),
            MakeForecast(new WeatherModel("m2"), (WindGusts, 18.0)),
            MakeForecast(new WeatherModel("m3"), (WindGusts, 10.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.True(alert.Confidence > 0.6);
    }

    [Fact]
    public void Storm_not_detected()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (WindGusts, 10.0)),
            MakeForecast(new WeatherModel("m2"), (WindGusts, 8.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Storm_severe_produces_orange()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (WindGusts, 28.0)),
            MakeForecast(new WeatherModel("m2"), (WindGusts, 26.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Storm_hurricane_produces_red()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (WindGusts, 36.0)),
            MakeForecast(new WeatherModel("m2"), (WindGusts, 34.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Storm_classifies_the_24h_maximum_not_the_minimum()
    {
        var consensus = Identical(2, h => [(WindGusts, h < 12 ? 10.0 : 26.0)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        var details = Assert.IsType<StormDetails>(alert.Details);
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(26.0, alert.TriggerValue),
            () => Assert.Equal(25.0, alert.Threshold),
            () => Assert.Equal(1.0, alert.Confidence),
            () => Assert.Null(alert.PeakValue),
            () => Assert.Equal(26.0, details.ExpectedMaxGust),
            () => Assert.Equal(2, details.ModelsAgreeing));
    }

    [Fact]
    public void Storm_reports_the_confidence_interval_upper_bound_as_peak_value()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (WindGusts, 20.0)),
            MakeForecast(new WeatherModel("m2"), (WindGusts, 21.0)),
            MakeForecast(new WeatherModel("m3"), (WindGusts, 22.0)),
            MakeForecast(new WeatherModel("m4"), (WindGusts, 30.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        var details = Assert.IsType<StormDetails>(alert.Details);
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(21.5, alert.TriggerValue),
            () => Assert.Equal(17.0, alert.Threshold),
            () => Assert.Equal(27.6, alert.PeakValue),
            () => Assert.Equal(0.75, alert.Confidence),
            () => Assert.Equal(4, details.ModelsAgreeing));
    }
}
