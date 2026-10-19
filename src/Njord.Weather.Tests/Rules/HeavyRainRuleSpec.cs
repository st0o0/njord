using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Alerts.Tests.Rules;

public sealed class HeavyRainRuleSpec
{
    private static readonly HeavyRainRule Rule = new();

    [Fact]
    public void Heavy_rain_single_hourly_exceedance_produces_yellow()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { Precipitation = h == 2 ? 12.0 : 0.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(12.0, alert.TriggerValue),
            () => Assert.Equal(10.0, alert.Threshold),
            () => Assert.Equal(2, alert.HoursUntil),
            () => Assert.Equal(new HeavyRainDetails(1, 0), alert.Details));
    }

    [Fact]
    public void Heavy_rain_daily_sum_without_hourly_exceedance_produces_orange()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Precipitation = 2.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(48.0, alert.TriggerValue),
            () => Assert.Equal(25.0, alert.Threshold),
            () => Assert.Null(alert.HoursUntil),
            () => Assert.Equal(new HeavyRainDetails(0, 1), alert.Details));
    }

    [Fact]
    public void Heavy_rain_hourly_and_daily_exceedance_produces_red()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { Precipitation = h < 3 ? 12.0 : 0.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(12.0, alert.TriggerValue),
            () => Assert.Equal(new HeavyRainDetails(3, 1), alert.Details));
    }

    [Fact]
    public void Heavy_rain_light_rain_produces_none()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { Precipitation = h < 4 ? 1.0 : 0.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.None, alert.Severity),
            () => Assert.Equal(4.0, alert.TriggerValue),
            () => Assert.Equal(25.0, alert.Threshold));
    }

    [Fact]
    public void Heavy_rain_takes_confidence_from_the_wettest_point()
    {
        var forecast = Day((h, p) => p with { Precipitation = h == 5 ? 15.0 : 0.0, EnsembleStdDev = h == 5 ? 0.3 : 6.0 });

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Equal(ConfidenceLevel.High, alert.Level);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void Heavy_rain_without_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.HeavyRain), Rule.Evaluate(Empty(), Defaults, Now));
    }
}
