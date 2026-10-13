using Njord.Alerts.Rules;
using Njord.Compute.Contracts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class StormRuleSpec
{
    private static readonly StormRule Rule = new();

    [Fact]
    public void Storm_detected()
    {
        var consensus = Consensus(
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
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (WindGusts, 10.0)),
            MakeForecast(new WeatherModel("m2"), (WindGusts, 8.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Storm_severe_produces_orange()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (WindGusts, 28.0)),
            MakeForecast(new WeatherModel("m2"), (WindGusts, 26.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Storm_hurricane_produces_red()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (WindGusts, 36.0)),
            MakeForecast(new WeatherModel("m2"), (WindGusts, 34.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }
}
