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

    [Theory]
    [InlineData(2.9, UvLevel.Low, AlertSeverity.None, 0.0)]
    [InlineData(3.0, UvLevel.Moderate, AlertSeverity.Yellow, 3.0)]
    [InlineData(5.9, UvLevel.Moderate, AlertSeverity.Yellow, 3.0)]
    [InlineData(6.0, UvLevel.High, AlertSeverity.Orange, 6.0)]
    [InlineData(7.9, UvLevel.High, AlertSeverity.Orange, 6.0)]
    [InlineData(8.0, UvLevel.VeryHigh, AlertSeverity.Red, 8.0)]
    [InlineData(10.9, UvLevel.VeryHigh, AlertSeverity.Red, 8.0)]
    [InlineData(11.0, UvLevel.Extreme, AlertSeverity.Red, 11.0)]
    public void Uv_level_boundaries_are_inclusive(double uv, UvLevel level, AlertSeverity severity, double threshold)
    {
        var consensus = Identical(2, _ => [(UvIndexParam, uv)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(threshold, alert.Threshold),
            () => Assert.Equal(uv, alert.TriggerValue),
            () => Assert.Equal(1.0, alert.Confidence),
            () => Assert.Equal(new UvDetails(level, uv), alert.Details));
    }

    [Fact]
    public void Uv_classifies_the_24h_maximum_not_the_minimum()
    {
        var consensus = Identical(2, h => [(UvIndexParam, h < 12 ? 2.0 : 7.0)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(new UvDetails(UvLevel.High, 7.0), alert.Details);
        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Uv_zero_everywhere_returns_the_none_alert()
    {
        var consensus = Identical(2, _ => [(UvIndexParam, 0.0)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.None, alert.Severity),
            () => Assert.Equal(0.0, alert.Confidence),
            () => Assert.Equal(0.0, alert.TriggerValue),
            () => Assert.Same(NoDetails.Instance, alert.Details));
    }

    [Fact]
    public void Uv_daily_maximum_takes_the_larger_of_today_and_tomorrow()
    {
        var consensus = ConsensusOf(
            MakeForecastWithDailySeries(new WeatherModel("m1"), [(DailyUvMax, [3.0, 9.0])]),
            MakeForecastWithDailySeries(new WeatherModel("m2"), [(DailyUvMax, [3.0, 9.0])]));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(new UvDetails(UvLevel.VeryHigh, 9.0), alert.Details);
        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }
}
