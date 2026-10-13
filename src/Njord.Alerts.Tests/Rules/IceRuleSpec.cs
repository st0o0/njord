using Njord.Alerts.Rules;
using Njord.Compute.Contracts;
using Njord.Compute.Settings;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class IceRuleSpec
{
    private static readonly IceRule Rule = new();
    private static readonly AlertSettings Settings = Defaults with { IceThreshold = 2.0 };

    [Fact]
    public void Ice_rain_at_near_freezing_produces_yellow()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Temperature, 1.5), (RainParam, 2.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 1.0), (RainParam, 1.5)));

        var alert = Rule.Evaluate(consensus, Settings);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.True(alert.Confidence > 0);
    }

    [Fact]
    public void Ice_freezing_rain_produces_orange()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Temperature, -1.0), (RainParam, 2.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, -0.5), (RainParam, 1.5)));

        var alert = Rule.Evaluate(consensus, Settings);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Ice_frozen_ground_produces_red()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Temperature, -1.0), (RainParam, 2.0), (SoilTemp0cm, -2.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, -0.5), (RainParam, 1.5), (SoilTemp0cm, -1.0)));

        var alert = Rule.Evaluate(consensus, Settings);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
        Assert.True(Assert.IsType<IceDetails>(alert.Details).SoilFrozen);
    }

    [Fact]
    public void Ice_snow_only_no_rain_produces_none()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Temperature, -3.0), (RainParam, 0.0), (Precipitation, 5.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, -2.0), (RainParam, 0.0), (Precipitation, 4.0)));

        var alert = Rule.Evaluate(consensus, Settings);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Ice_without_soil_temp_caps_at_orange()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Temperature, -1.0), (RainParam, 2.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, -0.5), (RainParam, 1.5)));

        var alert = Rule.Evaluate(consensus, Settings);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.False(Assert.IsType<IceDetails>(alert.Details).SoilFrozen);
    }
}
