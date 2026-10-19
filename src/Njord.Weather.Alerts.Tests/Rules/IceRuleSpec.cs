using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Alerts.Tests.Rules;

public sealed class IceRuleSpec
{
    private static readonly IceRule Rule = new();

    [Fact]
    public void Ice_rain_near_freezing_produces_yellow()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = 1.5, Rain = 2.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(2.0, alert.Threshold),
            () => Assert.Equal(new IceDetails(1.5, 24, false), alert.Details));
    }

    [Fact]
    public void Ice_freezing_rain_produces_orange()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = -1.0, Rain = 2.0 }), Defaults, Now);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.Equal(new IceDetails(-1.0, 24, false), alert.Details);
    }

    [Fact]
    public void Ice_frozen_ground_produces_red()
    {
        var alert = Rule.Evaluate(
            Uniform(p => p with { Temperature = -1.0, Rain = 2.0, SoilTemperature = -2.0 }), Defaults, Now);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.Equal(new IceDetails(-1.0, 24, true), alert.Details);
    }

    [Fact]
    public void Ice_snow_only_without_rain_returns_the_none_alert()
    {
        var alert = Rule.Evaluate(
            Uniform(p => p with { Temperature = -3.0, Rain = 0.0, Precipitation = 5.0 }), Defaults, Now);

        Assert.Equal(Alert.None(AlertType.Ice), alert);
    }

    [Fact]
    public void Ice_falls_back_to_precipitation_when_rain_is_unknown()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = -1.0, Precipitation = 1.0 }), Defaults, Now);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Ice_warm_rain_returns_the_none_alert()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = 8.0, Rain = 3.0 }), Defaults, Now);

        Assert.Equal(Alert.None(AlertType.Ice), alert);
    }

    [Fact]
    public void Ice_considers_the_hour_exactly_24_hours_ahead()
    {
        var forecast = Hourly(25, (h, p) => h == 24 ? p with { Temperature = -1.0, Rain = 1.0 } : p);

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(24, alert.HoursUntil),
            () => Assert.Equal(new IceDetails(-1.0, 1, false), alert.Details));
    }

    [Fact]
    public void Ice_ignores_the_hour_after_the_window()
    {
        var forecast = Hourly(26, (h, p) => h == 25 ? p with { Temperature = -1.0, Rain = 1.0 } : p);

        Assert.Equal(Alert.None(AlertType.Ice), Rule.Evaluate(forecast, Defaults, Now));
    }

    [Fact]
    public void Ice_takes_confidence_from_the_coldest_icy_point()
    {
        var forecast = Day((h, p) => p with
        {
            Temperature = h == 2 ? -4.0 : -1.0,
            Rain = 1.0,
            EnsembleStdDev = h == 2 ? 0.4 : 5.0,
        });

        var alert = Rule.Evaluate(forecast, Defaults, Now);

        Assert.Equal(ConfidenceLevel.High, alert.Level);
    }

    [Fact]
    public void Ice_without_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.Ice), Rule.Evaluate(Empty(), Defaults, Now));
    }
}
