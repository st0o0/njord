using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.FloodRain;
using Njord.Features.Alerts.FloodRisk;
using Njord.Features.Alerts.Pm25;
using Njord.Features.Alerts.Pollen;
using Njord.Features.Alerts.Thunderstorm;
using Njord.Features.Alerts.Uv;
using Njord.Messages.Alerts;
using Njord.Messages.Results.Alerts;
using Njord.Tests.Shared;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts;

public sealed class AlertThresholdConfigTests
{
    [Fact]
    public void Uv_validator_accepts_the_defaults()
    {
        Assert.True(new UvConfigValidator().Validate(null, new UvConfig { Thresholds = UvConfig.DefaultThresholds }).Succeeded);
    }

    [Fact]
    public void Uv_validator_rejects_three_values()
    {
        var result = new UvConfigValidator().Validate(null, new UvConfig { Thresholds = [3, 6, 8] });

        Assert.Contains("Njord:Features:Uv:Thresholds needs exactly four values", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Uv_validator_rejects_descending_values()
    {
        var result = new UvConfigValidator().Validate(null, new UvConfig { Thresholds = [11, 8, 6, 3] });

        Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Pm25_validator_accepts_the_defaults_and_rejects_descending_values()
    {
        Assert.Multiple(
            () => Assert.True(new Pm25ConfigValidator().Validate(null, new Pm25Config { Thresholds = Pm25Config.DefaultThresholds }).Succeeded),
            () => Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(new Pm25ConfigValidator().Validate(null, new Pm25Config { Thresholds = [50, 25, 15] }))));
    }

    [Fact]
    public void Pollen_validator_accepts_the_defaults_and_rejects_two_values()
    {
        Assert.Multiple(
            () => Assert.True(new PollenConfigValidator().Validate(null, new PollenConfig { Thresholds = PollenConfig.DefaultThresholds }).Succeeded),
            () => Assert.Contains("needs exactly three values", ValidationAssert.SingleFailure(new PollenConfigValidator().Validate(null, new PollenConfig { Thresholds = [20, 50] }))));
    }

    [Fact]
    public void Thunderstorm_validator_accepts_the_defaults_and_rejects_descending_values()
    {
        Assert.Multiple(
            () => Assert.True(new ThunderstormConfigValidator().Validate(null, new ThunderstormConfig { CapeThresholds = ThunderstormConfig.DefaultCapeThresholds }).Succeeded),
            () => Assert.Contains("Njord:Features:Thunderstorm:CapeThresholds must be strictly ascending", ValidationAssert.SingleFailure(new ThunderstormConfigValidator().Validate(null, new ThunderstormConfig { CapeThresholds = [2500, 1500, 1000] }))));
    }

    [Fact]
    public void Flood_rain_validator_rejects_a_non_positive_threshold_and_bad_days()
    {
        var validator = new FloodRainConfigValidator();

        Assert.Multiple(
            () => Assert.True(validator.Validate(null, new FloodRainConfig()).Succeeded),
            () => Assert.Contains("HeavyRainThreshold must be greater than 0", ValidationAssert.SingleFailure(validator.Validate(null, new FloodRainConfig { HeavyRainThreshold = 0 }))),
            () => Assert.Contains("ForecastDays must be between 1 and 210", ValidationAssert.SingleFailure(validator.Validate(null, new FloodRainConfig { ForecastDays = 211 }))),
            () => Assert.False(validator.Validate(null, new FloodRainConfig { ForecastDays = 0 }).Succeeded));
    }

    [Fact]
    public void Flood_risk_validator_rejects_a_factor_of_one_and_bad_days()
    {
        var validator = new FloodRiskConfigValidator();

        Assert.Multiple(
            () => Assert.True(validator.Validate(null, new FloodRiskConfig()).Succeeded),
            () => Assert.Contains("MedianFactor must be greater than 1", ValidationAssert.SingleFailure(validator.Validate(null, new FloodRiskConfig { MedianFactor = 1 }))),
            () => Assert.Contains("ForecastDays must be between 1 and 210", ValidationAssert.SingleFailure(validator.Validate(null, new FloodRiskConfig { ForecastDays = 0 }))));
    }

    [Fact]
    public void Custom_uv_thresholds_change_the_severity()
    {
        var rule = new UvAlert(Options.Create(new UvConfig { Thresholds = [1, 2, 3, 4] }));
        var result = Run(rule, Now, ForecastOnly(Uniform(p => p with { UvIndex = 3.5 })));

        var alert = Assert.IsType<UvAlertResult>(result);
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(3.0, alert.Threshold));
    }

    [Fact]
    public void Custom_pm25_thresholds_change_the_severity()
    {
        var rule = new Pm25Alert(Options.Create(new Pm25Config { Thresholds = [5, 10, 20] }));
        var result = Run(rule, Now, new LocationForecast(Lucerne, AirQuality: AirQualityFixture.Day((_, p) => p with { Pm25 = 12.0 })));

        var alert = Assert.IsType<Pm25AlertResult>(result);
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(10.0, alert.Threshold));
    }

    [Fact]
    public void Custom_pollen_thresholds_change_the_severity()
    {
        var rule = new PollenAlert(Options.Create(new PollenConfig { Thresholds = [1, 2, 3] }));
        var result = Run(rule, Now, new LocationForecast(Lucerne, AirQuality: AirQualityFixture.Day((_, p) => p with { BirchPollen = 5.0 })));

        var alert = Assert.IsType<PollenAlertResult>(result);
        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void A_lower_flood_rain_threshold_raises_the_alert_for_lighter_rain()
    {
        var forecast = new LocationForecast(Lucerne, FloodFixtures.Rain(1.2), Flood: FloodFixtures.FloodForecast(FloodFixtures.Point(0, 110), FloodFixtures.Point(1, 130)));

        Assert.Multiple(
            () => Assert.Null(Run(new FloodRainAlert(Options.Create(new FloodRainConfig { Enabled = true })), Now, forecast)),
            () => Assert.IsType<FloodRainAlertResult>(Run(new FloodRainAlert(Options.Create(new FloodRainConfig { Enabled = true, HeavyRainThreshold = 25 })), Now, forecast)));
    }

    [Fact]
    public void A_longer_flood_rain_window_sees_later_discharge_peaks()
    {
        var forecast = new LocationForecast(Lucerne, FloodFixtures.Rain(1.5), Flood: FloodFixtures.FloodForecast(FloodFixtures.Point(0, 110), FloodFixtures.Point(10, 130)));

        Assert.Multiple(
            () => Assert.Null(Run(new FloodRainAlert(Options.Create(new FloodRainConfig { Enabled = true })), Now, forecast)),
            () => Assert.IsType<FloodRainAlertResult>(Run(new FloodRainAlert(Options.Create(new FloodRainConfig { Enabled = true, ForecastDays = 14 })), Now, forecast)));
    }

    [Fact]
    public void A_lower_median_factor_raises_a_yellow_flood_risk()
    {
        var forecast = new LocationForecast(Lucerne, FloodFixtures.Rain(0.0), Flood: FloodFixtures.FloodForecast(FloodFixtures.Point(0, 190, median: 100, p75: 300)));

        var alert = Assert.IsType<FloodRiskAlertResult>(Run(new FloodRiskAlert(Options.Create(new FloodRiskConfig { Enabled = true, MedianFactor = 1.5 })), Now, forecast));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(150.0, alert.Threshold));
    }

    [Fact]
    public void A_longer_flood_risk_window_sees_later_days()
    {
        var forecast = new LocationForecast(Lucerne, FloodFixtures.Rain(0.0), Flood: FloodFixtures.FloodForecast(FloodFixtures.Point(10, 900)));

        Assert.Multiple(
            () => Assert.Null(Run(new FloodRiskAlert(Options.Create(new FloodRiskConfig { Enabled = true })), Now, forecast)),
            () => Assert.IsType<FloodRiskAlertResult>(Run(new FloodRiskAlert(Options.Create(new FloodRiskConfig { Enabled = true, ForecastDays = 14 })), Now, forecast)));
    }

    [Fact]
    public void Custom_cape_thresholds_change_the_thunderstorm_trigger()
    {
        var weather = Uniform(p => p with { Cape = 600, Precipitation = 0, WindGusts = 5 });
        var defaults = new ThunderstormAlert(Options.Create(new ThunderstormConfig { CapeThresholds = ThunderstormConfig.DefaultCapeThresholds }));
        var custom = new ThunderstormAlert(Options.Create(new ThunderstormConfig { CapeThresholds = [500, 1000, 2000] }));

        Assert.Multiple(
            () => Assert.Null(Run(defaults, Now, ForecastWithEnsemble(weather, EnsembleDay()))),
            () => Assert.IsType<ThunderstormAlertResult>(Run(custom, Now, ForecastWithEnsemble(weather, EnsembleDay()))));
    }
}
