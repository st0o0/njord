using Njord.Alerts.Rules;
using Njord.Compute.Contracts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class HeavyRainRuleSpec
{
    private static readonly HeavyRainRule Rule = new();

    [Fact]
    public void HeavyRain_below_threshold_preserves_trigger_value()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Precipitation, 0.5)),
            MakeForecast(new WeatherModel("m2"), (Precipitation, 0.3)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.True(alert.TriggerValue > 0);
    }

    [Fact]
    public void HeavyRain_hourly_and_daily_both_exceeded()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Precipitation, 12.0)),
            MakeForecast(new WeatherModel("m2"), (Precipitation, 11.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void HeavyRain_daily_sum_from_daily_series_triggers_alert()
    {
        var consensus = Consensus(
            MakeForecastWithDaily(new WeatherModel("m1"),
                [(Precipitation, 1.0)],
                [(DailyPrecipSum, 35.0)]),
            MakeForecastWithDaily(new WeatherModel("m2"),
                [(Precipitation, 1.0)],
                [(DailyPrecipSum, 40.0)]));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.True(alert.Severity >= AlertSeverity.Orange);
    }
}
