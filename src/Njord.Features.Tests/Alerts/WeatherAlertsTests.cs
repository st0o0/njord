using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.Fog;
using Njord.Features.Alerts.Frost;
using Njord.Features.Alerts.Heat;
using Njord.Features.Alerts.HeavyRain;
using Njord.Features.Alerts.Humidity;
using Njord.Features.Alerts.Ice;
using Njord.Features.Alerts.PressureDrop;
using Njord.Features.Alerts.Snow;
using Njord.Features.Alerts.Storm;
using Njord.Features.Alerts.Thunderstorm;
using Njord.Features.Alerts.TropicalNight;
using Njord.Features.Alerts.Uv;
using Njord.Features.Alerts.Visibility;
using Njord.Features.Alerts.WindChill;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts;

public sealed class WeatherAlertsTests
{
    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    private static AlertSeverity Severity(FeatureRule<WeatherInput> rule, DomainForecast<WeatherPoint> weather)
    {
        var result = Run(rule, Now, WeatherOnly(weather));
        return result is null
            ? AlertSeverity.None
            : Assert.IsAssignableFrom<AlertFeatureResult>(result).Severity;
    }

    private static AlertSeverity Severity(FeatureRule<WeatherEnsembleInput> rule, DomainForecast<WeatherPoint> weather)
    {
        var result = Run(rule, Now, WeatherWithEnsemble(weather, EnsembleDay()));
        return result is null
            ? AlertSeverity.None
            : Assert.IsAssignableFrom<AlertFeatureResult>(result).Severity;
    }

    [Theory]
    [InlineData(0.1, AlertSeverity.None)]
    [InlineData(0.0, AlertSeverity.Yellow)]
    [InlineData(-0.1, AlertSeverity.Yellow)]
    [InlineData(-5.0, AlertSeverity.Orange)]
    [InlineData(-15.0, AlertSeverity.Red)]
    public void Frost_severity_follows_the_lowest_temperature(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new FrostAlert(Opts(new FrostConfig())), Uniform(p => p with { Temperature = value })));

    [Theory]
    [InlineData(29.9, AlertSeverity.None)]
    [InlineData(30.0, AlertSeverity.Yellow)]
    [InlineData(35.0, AlertSeverity.Orange)]
    [InlineData(40.0, AlertSeverity.Red)]
    public void Heat_severity_follows_the_highest_temperature(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new HeatAlert(Opts(new HeatConfig())), Uniform(p => p with { Temperature = value })));

    [Theory]
    [InlineData(16.9, AlertSeverity.None)]
    [InlineData(17.0, AlertSeverity.Yellow)]
    [InlineData(25.0, AlertSeverity.Orange)]
    [InlineData(33.0, AlertSeverity.Red)]
    public void Storm_severity_follows_the_wind_gusts(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new StormAlert(Opts(new StormConfig())), Uniform(p => p with { WindGusts = value })));

    [Theory]
    [InlineData(-9.9, AlertSeverity.None)]
    [InlineData(-10.0, AlertSeverity.Yellow)]
    [InlineData(-20.0, AlertSeverity.Orange)]
    [InlineData(-30.0, AlertSeverity.Red)]
    public void Wind_chill_severity_follows_the_apparent_temperature(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new WindChillAlert(Opts(new WindChillConfig())), Uniform(p => p with { ApparentTemperature = value })));

    [Theory]
    [InlineData(1000.0, AlertSeverity.None)]
    [InlineData(999.0, AlertSeverity.Yellow)]
    [InlineData(199.0, AlertSeverity.Orange)]
    [InlineData(49.0, AlertSeverity.Red)]
    public void Visibility_severity_follows_the_lowest_visibility(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new VisibilityAlert(Opts(new VisibilityConfig())), Uniform(p => p with { Visibility = value })));

    [Theory]
    [InlineData(20.0, AlertSeverity.None)]
    [InlineData(20.1, AlertSeverity.Yellow)]
    [InlineData(23.1, AlertSeverity.Orange)]
    [InlineData(25.1, AlertSeverity.Red)]
    public void Tropical_night_severity_follows_the_night_temperature(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new TropicalNightAlert(Opts(new TropicalNightConfig())), Uniform(p => p with { IsDay = 0, Temperature = value })));

    [Theory]
    [InlineData(15.9, AlertSeverity.None)]
    [InlineData(16.0, AlertSeverity.Yellow)]
    [InlineData(21.0, AlertSeverity.Orange)]
    [InlineData(24.0, AlertSeverity.Red)]
    public void Humidity_severity_follows_the_daytime_dew_point(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new HumidityAlert(Opts(new HumidityConfig())), Uniform(p => p with { IsDay = 1, DewPoint = value })));

    [Theory]
    [InlineData(2.9, AlertSeverity.None)]
    [InlineData(3.0, AlertSeverity.Yellow)]
    [InlineData(6.0, AlertSeverity.Orange)]
    [InlineData(8.0, AlertSeverity.Red)]
    public void Uv_severity_follows_the_uv_index(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new UvAlert(Opts(new UvConfig())), Uniform(p => p with { UvIndex = value })));

    [Theory]
    [InlineData(9.9, AlertSeverity.None)]
    [InlineData(10.0, AlertSeverity.Yellow)]
    [InlineData(25.0, AlertSeverity.Red)]
    public void Heavy_rain_severity_follows_hourly_and_daily_precipitation(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new HeavyRainAlert(Opts(new HeavyRainConfig())), Day((h, p) => h == 0 ? p with { Precipitation = value } : p)));

    [Theory]
    [InlineData(2.1, AlertSeverity.None)]
    [InlineData(2.0, AlertSeverity.Yellow)]
    [InlineData(0.0, AlertSeverity.Orange)]
    public void Ice_severity_follows_the_temperature_during_precipitation(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new IceAlert(Opts(new IceConfig())), Uniform(p => p with { Precipitation = 1.0, Temperature = value })));

    [Theory]
    [InlineData(4.9, AlertSeverity.None)]
    [InlineData(5.0, AlertSeverity.Yellow)]
    [InlineData(10.0, AlertSeverity.Orange)]
    public void Pressure_drop_severity_follows_the_drop(double drop, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new PressureDropAlert(Opts(new PressureDropConfig())), Day((h, p) => p with { Pressure = h < 3 ? 1013.0 : 1013.0 - drop })));

    [Theory]
    [InlineData(0.0, AlertSeverity.None)]
    [InlineData(0.5, AlertSeverity.Yellow)]
    [InlineData(6.0, AlertSeverity.Orange)]
    [InlineData(21.0, AlertSeverity.Red)]
    public void Snow_severity_follows_the_accumulation(double value, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new SnowAlert(Opts(new SnowConfig())), Day((h, p) => p with { Snowfall = h == 0 ? value : 0.0 })));

    [Theory]
    [InlineData(1000.0, AlertSeverity.None)]
    [InlineData(1001.0, AlertSeverity.Red)]
    public void Thunderstorm_severity_follows_cape_when_rain_and_gusts_agree(double cape, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new ThunderstormAlert(Opts(new ThunderstormConfig())), Uniform(p => p with { Cape = cape, Precipitation = 5.1, WindGusts = 15.1 })));

    [Theory]
    [InlineData(0, AlertSeverity.None)]
    [InlineData(3, AlertSeverity.Yellow)]
    [InlineData(4, AlertSeverity.Orange)]
    public void Fog_severity_follows_the_persistence_of_foggy_hours(int foggyHours, AlertSeverity expected) =>
        Assert.Equal(expected, Severity(new FogAlert(Opts(new FogConfig())),
            Day((h, p) => h < foggyHours ? p with { Temperature = 5.0, DewPoint = 4.5, WindSpeed = 1.0, Humidity = 95.0 } : p)));

    [Fact]
    public void Fog_needs_a_small_gap_between_temperature_and_dew_point()
    {
        var weather = Uniform(p => p with { Temperature = 5.0, DewPoint = 1.0, WindSpeed = 1.0, Humidity = 95.0 });

        Assert.Equal(AlertSeverity.None, Severity(new FogAlert(Opts(new FogConfig())), weather));
    }

    [Fact]
    public void A_cold_forecast_produces_an_orange_frost_alert_for_the_location()
    {
        var frost = Assert.IsType<FrostAlertResult>(
            Run(new FrostAlert(Opts(new FrostConfig())), Now, WeatherWithEnsemble(Uniform(p => p with { Temperature = -6.0 }), EnsembleDay())));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, frost.Severity),
            () => Assert.Equal(Lucerne, frost.Location));
    }

    [Fact]
    public void Missing_weather_produces_no_alert()
    {
        Assert.Null(Run(new FrostAlert(Opts(new FrostConfig())), Now, new LocationForecast(Lucerne)));
    }

    [Fact]
    public void Missing_ensemble_produces_no_alert_for_ensemble_requiring_alerts()
    {
        Assert.Null(Run(new FrostAlert(Opts(new FrostConfig())), Now, WeatherOnly(Uniform(p => p with { Temperature = -6.0 }))));
    }

    [Fact]
    public void Independent_features_alert_independently_on_the_same_forecast()
    {
        var weather = Hourly(48, (h, p) => h switch
        {
            2 => p with { Temperature = -6.0, ApparentTemperature = -6.0 },
            3 => p with { Temperature = 40.0, ApparentTemperature = 40.0 },
            _ => p,
        });
        var forecast = WeatherWithEnsemble(weather, EnsembleHourly(48));

        Assert.Multiple(
            () => Assert.IsType<FrostAlertResult>(Run(new FrostAlert(Opts(new FrostConfig())), Now, forecast)),
            () => Assert.IsType<HeatAlertResult>(Run(new HeatAlert(Opts(new HeatConfig())), Now, forecast)));
    }

    [Fact]
    public void A_disabled_feature_reports_itself_as_disabled()
    {
        Assert.False(new FrostAlert(Opts(new FrostConfig { Enabled = false })).Enabled);
    }

    [Fact]
    public void The_seventeen_weather_alerts_are_alert_features_of_the_weather_category()
    {
        var alerts = FeatureServices.AllFeatureRules()
            .Where(rule => rule.Descriptor is { Output: OutputKind.Alert, Category: FeatureCategory.Weather })
            .ToList();

        Assert.Equal(17, alerts.Count);
    }
}
