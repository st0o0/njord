using Njord.Messages.Alerts;
using Njord.Model;
using Njord.Tests.Shared;
using static Njord.AirQuality.Tests.AirQualityFixture;

namespace Njord.AirQuality.Tests;

public sealed class PollenAlertRuleSpec
{
    private static readonly PollenAlertRule Rule = new();

    [Theory]
    [InlineData(19.9, AlertSeverity.None, 0.0)]
    [InlineData(20.0, AlertSeverity.Yellow, 20.0)]
    [InlineData(49.9, AlertSeverity.Yellow, 20.0)]
    [InlineData(50.0, AlertSeverity.Orange, 50.0)]
    [InlineData(100.0, AlertSeverity.Red, 100.0)]
    public void Severity_boundaries_are_inclusive(double value, AlertSeverity severity, double threshold)
    {
        var alert = Rule.Evaluate(Day((_, p) => p with { BirchPollen = value }), AlertRuleFixture.Defaults, Now);

        Assert.Equal(severity, alert.Severity);
        Assert.Equal(threshold, alert.Threshold);
    }

    [Theory]
    [InlineData("birch")]
    [InlineData("grass")]
    [InlineData("alder")]
    [InlineData("mugwort")]
    public void Any_pollen_type_can_trigger_the_alert(string type)
    {
        var forecast = Day((h, p) => h != 6 ? p : type switch
        {
            "birch" => p with { BirchPollen = 60.0 },
            "grass" => p with { GrassPollen = 60.0 },
            "alder" => p with { Alder = 60.0 },
            _ => p with { Mugwort = 60.0 },
        });

        var alert = Rule.Evaluate(forecast, AlertRuleFixture.Defaults, Now);

        Assert.Equal(UnifiedAlert.Of(AlertType.Pollen, AlertSeverity.Orange, 60.0, 50.0, 6), alert);
    }

    [Fact]
    public void The_peak_over_all_types_and_hours_is_reported()
    {
        var forecast = Day((h, p) => p with
        {
            BirchPollen = h == 3 ? 30.0 : 1.0,
            GrassPollen = h == 9 ? 120.0 : 2.0,
        });

        var alert = Rule.Evaluate(forecast, AlertRuleFixture.Defaults, Now);

        Assert.Equal(UnifiedAlert.Of(AlertType.Pollen, AlertSeverity.Red, 120.0, 100.0, 9), alert);
    }

    [Fact]
    public void Missing_pollen_data_returns_the_none_alert()
    {
        Assert.Equal(Alert.None(AlertType.Pollen), Rule.Evaluate(Day(), AlertRuleFixture.Defaults, Now));
    }

    [Fact]
    public void Points_beyond_the_next_24_hours_are_ignored()
    {
        var forecast = Day((_, p) => p with { BirchPollen = 5.0 });
        var late = new AirQualityPoint(Now.AddHours(30), 5.0, 10.0, 40.0, BirchPollen: 500.0);
        forecast = forecast with { Points = forecast.Points.Add(late) };

        Assert.Equal(AlertType.Pollen, Rule.Evaluate(forecast, AlertRuleFixture.Defaults, Now).Type);
        Assert.Equal(AlertSeverity.None, Rule.Evaluate(forecast, AlertRuleFixture.Defaults, Now).Severity);
    }
}
