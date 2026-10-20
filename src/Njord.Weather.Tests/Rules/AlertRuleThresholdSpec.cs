using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class AlertRuleThresholdSpec
{
    private sealed record Scenario(
        IAlertRule<DomainForecast<WeatherPoint>> Rule,
        AlertType Type,
        Func<double, DomainForecast<WeatherPoint>> Forecast);

    // Each scenario shapes the forecast from one driving value; the rows below pin it around every default threshold.
    private static readonly Dictionary<string, Scenario> Scenarios = new(StringComparer.Ordinal)
    {
        ["Frost"] = new(new FrostRule(), AlertType.Frost, v => Uniform(p => p with { Temperature = v })),
        ["Heat"] = new(new HeatRule(), AlertType.Heat, v => Uniform(p => p with { Temperature = v })),
        ["Storm"] = new(new StormRule(), AlertType.Storm, v => Uniform(p => p with { WindGusts = v })),
        ["WindChill"] = new(new WindChillRule(), AlertType.WindChill, v => Uniform(p => p with { ApparentTemperature = v })),
        ["Visibility"] = new(new VisibilityRule(), AlertType.Visibility, v => Uniform(p => p with { Visibility = v })),
        ["TropicalNight"] = new(new TropicalNightRule(), AlertType.TropicalNight,
            v => Uniform(p => p with { IsDay = false, Temperature = v })),
        ["Humidity"] = new(new HumidityRule(), AlertType.Humidity, v => Uniform(p => p with { IsDay = true, DewPoint = v })),
        ["Uv"] = new(new UvRule(), AlertType.Uv, v => Uniform(p => p with { UvIndex = v })),
        ["HeavyRain"] = new(new HeavyRainRule(), AlertType.HeavyRain,
            v => Day((h, p) => h == 0 ? p with { Precipitation = v } : p)),
        ["Ice"] = new(new IceRule(), AlertType.Ice, v => Uniform(p => p with { Precipitation = 1.0, Temperature = v })),
        ["PressureDrop"] = new(new PressureDropRule(), AlertType.PressureDrop,
            v => Day((h, p) => p with { Pressure = h < 3 ? 1013.0 : 1013.0 - v })),
        ["Snow"] = new(new SnowRule(), AlertType.Snow, v => Day((h, p) => p with { Snowfall = h == 0 ? v : 0.0 })),
        ["Thunderstorm"] = new(new ThunderstormRule(), AlertType.Thunderstorm,
            v => Uniform(p => p with { Cape = v, Precipitation = 5.1, WindGusts = 15.1 })),
        ["Fog"] = new(new FogRule(), AlertType.Fog,
            v => Day((h, p) => h < v
                ? p with { Temperature = 5.0, DewPoint = 4.5, WindSpeed = 1.0, Humidity = 95.0 }
                : p)),
    };

    [Theory]
    // Frost: descending, inclusive
    [InlineData("Frost", 0.1, AlertSeverity.None)]
    [InlineData("Frost", 0.0, AlertSeverity.Yellow)]
    [InlineData("Frost", -0.1, AlertSeverity.Yellow)]
    [InlineData("Frost", -5.0, AlertSeverity.Orange)]
    [InlineData("Frost", -15.0, AlertSeverity.Red)]
    // Heat: ascending, inclusive
    [InlineData("Heat", 29.9, AlertSeverity.None)]
    [InlineData("Heat", 30.0, AlertSeverity.Yellow)]
    [InlineData("Heat", 35.0, AlertSeverity.Orange)]
    [InlineData("Heat", 40.0, AlertSeverity.Red)]
    // Storm gusts in m/s
    [InlineData("Storm", 16.9, AlertSeverity.None)]
    [InlineData("Storm", 17.0, AlertSeverity.Yellow)]
    [InlineData("Storm", 25.0, AlertSeverity.Orange)]
    [InlineData("Storm", 33.0, AlertSeverity.Red)]
    // Wind chill on the apparent temperature
    [InlineData("WindChill", -9.9, AlertSeverity.None)]
    [InlineData("WindChill", -10.0, AlertSeverity.Yellow)]
    [InlineData("WindChill", -20.0, AlertSeverity.Orange)]
    [InlineData("WindChill", -30.0, AlertSeverity.Red)]
    // Visibility: strict, below the threshold
    [InlineData("Visibility", 1000.0, AlertSeverity.None)]
    [InlineData("Visibility", 999.0, AlertSeverity.Yellow)]
    [InlineData("Visibility", 199.0, AlertSeverity.Orange)]
    [InlineData("Visibility", 49.0, AlertSeverity.Red)]
    // Tropical night: strict, above the threshold
    [InlineData("TropicalNight", 20.0, AlertSeverity.None)]
    [InlineData("TropicalNight", 20.1, AlertSeverity.Yellow)]
    [InlineData("TropicalNight", 23.1, AlertSeverity.Orange)]
    [InlineData("TropicalNight", 25.1, AlertSeverity.Red)]
    // Humidity on the daytime dew point
    [InlineData("Humidity", 15.9, AlertSeverity.None)]
    [InlineData("Humidity", 16.0, AlertSeverity.Yellow)]
    [InlineData("Humidity", 21.0, AlertSeverity.Orange)]
    [InlineData("Humidity", 24.0, AlertSeverity.Red)]
    // UV index bands
    [InlineData("Uv", 2.9, AlertSeverity.None)]
    [InlineData("Uv", 3.0, AlertSeverity.Yellow)]
    [InlineData("Uv", 6.0, AlertSeverity.Orange)]
    [InlineData("Uv", 8.0, AlertSeverity.Red)]
    // Heavy rain: one hour at the hourly threshold, below the daily sum
    [InlineData("HeavyRain", 9.9, AlertSeverity.None)]
    [InlineData("HeavyRain", 10.0, AlertSeverity.Yellow)]
    [InlineData("HeavyRain", 25.0, AlertSeverity.Red)]
    // Ice: rain at or below the ice threshold; at or below zero it is orange
    [InlineData("Ice", 2.1, AlertSeverity.None)]
    [InlineData("Ice", 2.0, AlertSeverity.Yellow)]
    [InlineData("Ice", 0.0, AlertSeverity.Orange)]
    // Pressure drop over three hours
    [InlineData("PressureDrop", 4.9, AlertSeverity.None)]
    [InlineData("PressureDrop", 5.0, AlertSeverity.Yellow)]
    [InlineData("PressureDrop", 10.0, AlertSeverity.Orange)]
    // Snow accumulation in cm
    [InlineData("Snow", 0.0, AlertSeverity.None)]
    [InlineData("Snow", 0.5, AlertSeverity.Yellow)]
    [InlineData("Snow", 6.0, AlertSeverity.Orange)]
    [InlineData("Snow", 21.0, AlertSeverity.Red)]
    // Thunderstorm: CAPE must exceed its threshold in every hour
    [InlineData("Thunderstorm", 1000.0, AlertSeverity.None)]
    [InlineData("Thunderstorm", 1001.0, AlertSeverity.Red)]
    // Fog: persistence in hours
    [InlineData("Fog", 0.0, AlertSeverity.None)]
    [InlineData("Fog", 3.0, AlertSeverity.Yellow)]
    [InlineData("Fog", 4.0, AlertSeverity.Orange)]
    public void A_rule_classifies_the_driving_value_by_its_default_thresholds(
        string rule, double value, AlertSeverity expected)
    {
        var scenario = Scenarios[rule];

        var alert = scenario.Rule.Evaluate(scenario.Forecast(value), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(scenario.Type, alert.Type),
            () => Assert.Equal(expected, alert.Severity));
    }

    [Fact]
    public void Fog_is_not_raised_when_the_dew_point_spread_is_wide()
    {
        var forecast = Uniform(p => p with { Temperature = 5.0, DewPoint = 1.0, WindSpeed = 1.0, Humidity = 95.0 });

        var alert = new FogRule().Evaluate(forecast, Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
