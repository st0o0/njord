using Njord.Alerts.Rules;
using Njord.Compute.Contracts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class VisibilityRuleSpec
{
    private static readonly VisibilityRule Rule = new();

    [Fact]
    public void Visibility_reduced_produces_yellow()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (VisibilityParam, 800.0)),
            MakeForecast(new WeatherModel("m2"), (VisibilityParam, 900.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void Visibility_dense_fog_produces_orange()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (VisibilityParam, 150.0)),
            MakeForecast(new WeatherModel("m2"), (VisibilityParam, 180.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Visibility_near_zero_produces_red()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (VisibilityParam, 30.0)),
            MakeForecast(new WeatherModel("m2"), (VisibilityParam, 40.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Visibility_good_produces_none()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (VisibilityParam, 5000.0)),
            MakeForecast(new WeatherModel("m2"), (VisibilityParam, 8000.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
