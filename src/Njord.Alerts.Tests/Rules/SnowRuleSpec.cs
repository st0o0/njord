using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using Njord.Model;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class SnowRuleSpec
{
    private static readonly SnowRule Rule = new();

    [Fact]
    public void Snow_light()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Snowfall, 0.1)),
            MakeForecast(new WeatherModel("m2"), (Snowfall, 0.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void Snow_daily_sum_increases_severity()
    {
        var consensus = ConsensusOf(
            MakeForecastWithDaily(new WeatherModel("m1"),
                [(Snowfall, 0.1)],
                [(DailySnowfallSum, 8.0)]),
            MakeForecastWithDaily(new WeatherModel("m2"),
                [(Snowfall, 0.1)],
                [(DailySnowfallSum, 7.0)]));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Snow_sums_the_hourly_medians()
    {
        var consensus = Identical(2, _ => [(Snowfall, 1.0)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(24.0, alert.TriggerValue),
            () => Assert.Equal(20.0, alert.Threshold),
            () => Assert.Equal(24.0, Assert.IsType<SnowDetails>(alert.Details).ExpectedAccumulation));
    }

    [Theory]
    [InlineData(0.5, AlertSeverity.Yellow, 0.0)]
    [InlineData(5.0, AlertSeverity.Yellow, 0.0)]
    [InlineData(6.0, AlertSeverity.Orange, 5.0)]
    [InlineData(20.0, AlertSeverity.Orange, 5.0)]
    [InlineData(21.0, AlertSeverity.Red, 20.0)]
    public void Snow_accumulation_boundaries_are_exclusive(double accumulation, AlertSeverity severity, double threshold)
    {
        var consensus = Identical(2, h => [(Snowfall, h == 5 ? accumulation : 0.0)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(threshold, alert.Threshold),
            () => Assert.Equal(accumulation, alert.TriggerValue),
            () => Assert.Equal(1.0, alert.Confidence));
    }

    [Fact]
    public void Snow_confidence_averages_only_the_hours_with_snowfall()
    {
        static Func<int, (ParameterDef, double)[]> Hourly(double atHourThree) =>
            h => [(Snowfall, h == 3 ? atHourThree : h == 4 ? 0.5 : 0.0)];
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), Hourly(0.5)),
            MakeForecast(new WeatherModel("m2"), Hourly(0.5)),
            MakeForecast(new WeatherModel("m3"), Hourly(5.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        var details = Assert.IsType<SnowDetails>(alert.Details);
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(0.833, alert.Confidence),
            () => Assert.Equal(1.0, alert.TriggerValue),
            () => Assert.Null(details.FreezingLevel),
            () => Assert.Equal(3, details.ModelsAgreeing));
    }

    [Fact]
    public void Snow_reports_the_median_freezing_level_rounded_to_whole_metres()
    {
        var consensus = Identical(2, h => [(Snowfall, 1.0), (ParameterRegistry.FreezingLevelHeight, 1000.0 + h * 10.0)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(1115.0, Assert.IsType<SnowDetails>(alert.Details).FreezingLevel);
    }
}
