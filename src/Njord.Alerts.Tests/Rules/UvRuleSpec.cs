using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class UvRuleSpec
{
    private static readonly UvRule Rule = new();

    [Fact]
    public void Uv_high()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (UvIndexParam, 7.5)),
            MakeForecast(new WeatherModel("m2"), (UvIndexParam, 8.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.Equal(UvLevel.High, Assert.IsType<UvDetails>(alert.Details).Level);
    }

    [Fact]
    public void Uv_low()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (UvIndexParam, 2.0)),
            MakeForecast(new WeatherModel("m2"), (UvIndexParam, 1.5)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(UvLevel.Low, Assert.IsType<UvDetails>(alert.Details).Level);
    }

    [Fact]
    public void Uv_daily_max_higher_than_hourly_peak()
    {
        var consensus = ConsensusOf(
            MakeForecastWithDaily(new WeatherModel("m1"),
                [(UvIndexParam, 5.0)],
                [(DailyUvMax, 9.0)]),
            MakeForecastWithDaily(new WeatherModel("m2"),
                [(UvIndexParam, 4.0)],
                [(DailyUvMax, 8.5)]));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.Equal(UvLevel.VeryHigh, Assert.IsType<UvDetails>(alert.Details).Level);
    }

    [Fact]
    public void Uv_daily_not_available_falls_back_to_hourly()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (UvIndexParam, 7.0)),
            MakeForecast(new WeatherModel("m2"), (UvIndexParam, 6.5)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }
}
