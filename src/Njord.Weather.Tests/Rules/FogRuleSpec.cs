using Njord.Messages.Alerts;
using Njord.Model;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class FogRuleSpec
{
    private static readonly FogRule Rule = new();

    private static WeatherPoint Foggy(WeatherPoint p) =>
        p with { Temperature = 5.0, DewPoint = 4.5, WindSpeed = 1.0, Humidity = 95.0 };

    [Theory]
    [InlineData(2, AlertSeverity.Yellow)]
    [InlineData(3, AlertSeverity.Yellow)]
    [InlineData(4, AlertSeverity.Orange)]
    [InlineData(10, AlertSeverity.Orange)]
    public void Fog_becomes_persistent_at_the_configured_hours(int foggyHours, AlertSeverity severity)
    {
        var alert = Rule.Evaluate(Day((h, p) => h >= 5 && h < 5 + foggyHours ? Foggy(p) : p), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(foggyHours, alert.TriggerValue),
            () => Assert.Equal(1.0, alert.Threshold),
            () => Assert.Equal(5, alert.HoursUntil),
            () => Assert.Equal(foggyHours, alert.DurationHours),
            () => Assert.Equal(new FogDetails(foggyHours, 0), alert.Details));
    }

    [Fact]
    public void Fog_uses_the_passed_persistence_setting()
    {
        var alert = Rule.Evaluate(Uniform(Foggy), Defaults with { FogPersistentHours = 25 }, Now);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void Fog_clear_weather_produces_none_without_duration()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = 20.0, DewPoint = 10.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.None, alert.Severity),
            () => Assert.Equal(0.0, alert.TriggerValue),
            () => Assert.Null(alert.HoursUntil),
            () => Assert.Null(alert.DurationHours));
    }

    [Theory]
    [InlineData(3.0, 95.0, 4.5)]
    [InlineData(1.0, 90.0, 4.5)]
    [InlineData(1.0, 95.0, 3.0)]
    public void Fog_conditions_are_exclusive_at_their_limits(double wind, double humidity, double dewPoint)
    {
        var alert = Rule.Evaluate(
            Uniform(p => p with { Temperature = 5.0, DewPoint = dewPoint, WindSpeed = wind, Humidity = humidity }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Fog_takes_confidence_from_the_first_foggy_point()
    {
        var forecast = Day((h, p) => Foggy(p) with { EnsembleStdDev = h == 0 ? 0.5 : 5.0 });

        Assert.Equal(ConfidenceLevel.High, Rule.Evaluate(forecast, Defaults, Now).Level);
    }

    [Fact]
    public void Fog_without_data_produces_none()
    {
        var alert = Rule.Evaluate(Empty(), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(new FogDetails(0, 0), alert.Details);
    }
}
