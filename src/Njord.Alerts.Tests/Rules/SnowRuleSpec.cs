using Njord.Alerts.Rules;
using Njord.Compute.Contracts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class SnowRuleSpec
{
    private static readonly SnowRule Rule = new();

    [Fact]
    public void Snow_light()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Snowfall, 0.1)),
            MakeForecast(new WeatherModel("m2"), (Snowfall, 0.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void Snow_daily_sum_increases_severity()
    {
        var consensus = Consensus(
            MakeForecastWithDaily(new WeatherModel("m1"),
                [(Snowfall, 0.1)],
                [(DailySnowfallSum, 8.0)]),
            MakeForecastWithDaily(new WeatherModel("m2"),
                [(Snowfall, 0.1)],
                [(DailySnowfallSum, 7.0)]));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }
}
