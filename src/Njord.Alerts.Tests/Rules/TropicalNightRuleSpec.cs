using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class TropicalNightRuleSpec
{
    private static readonly TropicalNightRule Rule = new();

    [Fact]
    public void TropicalNight_warm_night_produces_yellow()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Temperature, 21.0), (IsDayParam, 0.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 22.0), (IsDayParam, 0.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void TropicalNight_severe_produces_orange()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Temperature, 24.0), (IsDayParam, 0.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 24.5), (IsDayParam, 0.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void TropicalNight_extreme_produces_red()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Temperature, 26.0), (IsDayParam, 0.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 27.0), (IsDayParam, 0.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void TropicalNight_cool_night_produces_none()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Temperature, 15.0), (IsDayParam, 0.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 14.0), (IsDayParam, 0.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void TropicalNight_all_daytime_produces_none()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Temperature, 25.0), (IsDayParam, 1.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 26.0), (IsDayParam, 1.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
