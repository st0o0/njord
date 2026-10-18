using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using Njord.Model;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class HeavyRainRuleSpec
{
    private static readonly HeavyRainRule Rule = new();

    [Fact]
    public void HeavyRain_below_threshold_preserves_trigger_value()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Precipitation, 0.5)),
            MakeForecast(new WeatherModel("m2"), (Precipitation, 0.3)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.True(alert.TriggerValue > 0);
    }

    [Fact]
    public void HeavyRain_hourly_and_daily_both_exceeded()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Precipitation, 12.0)),
            MakeForecast(new WeatherModel("m2"), (Precipitation, 11.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void HeavyRain_daily_sum_from_daily_series_triggers_alert()
    {
        var consensus = ConsensusOf(
            MakeForecastWithDaily(new WeatherModel("m1"),
                [(Precipitation, 1.0)],
                [(DailyPrecipSum, 35.0)]),
            MakeForecastWithDaily(new WeatherModel("m2"),
                [(Precipitation, 1.0)],
                [(DailyPrecipSum, 40.0)]));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.True(alert.Severity >= AlertSeverity.Orange);
    }

    [Fact]
    public void HeavyRain_hourly_value_exactly_at_threshold_counts()
    {
        var consensus = Identical(2, h => [(Precipitation, h == 5 ? 10.0 : 0.0)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(10.0, alert.TriggerValue),
            () => Assert.Equal(10.0, alert.Threshold),
            () => Assert.Equal(1.0, alert.Confidence),
            () => Assert.Equal(new HeavyRainDetails(1, 0), alert.Details));
    }

    [Fact]
    public void HeavyRain_reports_the_largest_hourly_value_when_both_limits_are_exceeded()
    {
        var consensus = Identical(2, h => [(Precipitation, h == 5 ? 12.0 : h == 6 ? 15.0 : 0.0)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(15.0, alert.TriggerValue),
            () => Assert.Equal(10.0, alert.Threshold),
            () => Assert.Equal(new HeavyRainDetails(2, 1), alert.Details));
    }

    [Fact]
    public void HeavyRain_daily_sum_exactly_at_threshold_is_orange_with_full_confidence()
    {
        var consensus = ConsensusOf(
            MakeForecastWithDaily(new WeatherModel("m1"), [(Precipitation, 0.5)], [(DailyPrecipSum, 25.0)]),
            MakeForecastWithDaily(new WeatherModel("m2"), [(Precipitation, 0.5)], [(DailyPrecipSum, 25.0)]));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(25.0, alert.TriggerValue),
            () => Assert.Equal(25.0, alert.Threshold),
            () => Assert.Equal(1.0, alert.Confidence),
            () => Assert.Equal(new HeavyRainDetails(0, 1), alert.Details));
    }

    [Fact]
    public void HeavyRain_confidence_is_the_average_agreement_of_the_hourly_hits()
    {
        static Func<int, (ParameterDef, double)[]> Hourly(double atHourFive) =>
            h => [(Precipitation, h == 5 ? atHourFive : h == 6 ? 12.0 : 0.0)];
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), Hourly(12.0)),
            MakeForecast(new WeatherModel("m2"), Hourly(12.0)),
            MakeForecast(new WeatherModel("m3"), Hourly(20.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(0.833, alert.Confidence),
            () => Assert.Equal(12.0, alert.TriggerValue),
            () => Assert.Equal(new HeavyRainDetails(2, 0), alert.Details));
    }

    [Fact]
    public void HeavyRain_below_both_limits_reports_the_daily_sum_against_the_daily_threshold()
    {
        var consensus = Identical(2, _ => [(Precipitation, 0.5)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.None, alert.Severity),
            () => Assert.Equal(0.0, alert.Confidence),
            () => Assert.Equal(12.0, alert.TriggerValue),
            () => Assert.Equal(25.0, alert.Threshold),
            () => Assert.Equal(new HeavyRainDetails(0, 0), alert.Details));
    }
}
