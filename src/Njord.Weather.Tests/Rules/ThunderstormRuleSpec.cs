using Njord.Messages.Alerts;
using Njord.Model;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class ThunderstormRuleSpec
{
    private static readonly ThunderstormRule Rule = new();

    private static WeatherPoint Stormy(WeatherPoint p) => p with { Cape = 1500.0, WindGusts = 20.0, Precipitation = 6.0 };

    [Theory]
    [InlineData(24, AlertSeverity.Red)]
    [InlineData(18, AlertSeverity.Orange)]
    [InlineData(12, AlertSeverity.Orange)]
    [InlineData(6, AlertSeverity.Yellow)]
    [InlineData(0, AlertSeverity.None)]
    public void Thunderstorm_severity_follows_the_share_of_stormy_hours(int stormyHours, AlertSeverity severity)
    {
        var forecast = Day((h, p) => h < stormyHours ? Stormy(p) : p with { Cape = 200.0, WindGusts = 5.0 });

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Equal(severity, alert.Severity);
    }

    [Fact]
    public void Thunderstorm_reports_peak_cape_threshold_and_first_hour()
    {
        var forecast = Day((h, p) => h >= 4 ? Stormy(p) with { Cape = 1500.0 + h } : p with { Cape = 100.0, WindGusts = 5.0 });

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(1523.0, alert.TriggerValue),
            () => Assert.Equal(1000.0, alert.Threshold),
            () => Assert.Equal(4, alert.HoursUntil),
            () => Assert.Equal(new ThunderstormDetails(0), alert.Details));
    }

    [Fact]
    public void Thunderstorm_requires_all_three_conditions()
    {
        var alert = Rule.Evaluate(Uniform(p => Stormy(p) with { Precipitation = 1.0 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Null(alert.HoursUntil);
    }

    [Fact]
    public void Thunderstorm_takes_confidence_from_the_highest_cape_point()
    {
        var forecast = Day((h, p) => Stormy(p) with { Cape = h == 9 ? 3000.0 : 1500.0, EnsembleStdDev = h == 9 ? 0.2 : 5.0 });

        Assert.Equal(ConfidenceLevel.High, Rule.Evaluate(forecast, Defaults, Now).Level);
    }

    [Fact]
    public void Thunderstorm_without_cape_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.Thunderstorm), Rule.Evaluate(Day(), Defaults, Now));
    }

    [Fact]
    public void Thunderstorm_without_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.Thunderstorm), Rule.Evaluate(Empty(), Defaults, Now));
    }
}
