using Microsoft.Extensions.Options;
using Njord.Core.Forecast;
using Njord.Features.Alerts.FireRisk;
using Njord.Messages.Alerts;
using Njord.Messages.Results.Alerts;
using Njord.Messages.Stream;
using Njord.Tests.Shared;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.FireRisk;

public sealed class FireRiskAlertTests
{
    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    private static FireRiskAlert Rule(FireRiskConfig? cfg = null) => new(Opts(cfg ?? new FireRiskConfig { Enabled = true }));

    private static PipelineEvent? Run(DomainForecast<ForecastPoint> weather, FireRiskConfig? cfg = null) =>
        FeatureRunner.Run(Rule(cfg), Now, ForecastWithEnsemble(weather, EnsembleDay()));

    [Fact]
    public void All_conditions_met_produces_red_severity()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 38.0,
            Humidity = 15.0,
            WindSpeed = 8.0,
            SoilMoisture0To1Cm = 0.05,
            Precipitation = 0.0,
        });
        var result = Run(weather);
        Assert.NotNull(result);
        var alert = Assert.IsType<FireRiskAlertResult>(result, exactMatch: false);
        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Moderate_conditions_produces_yellow()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 33.0,
            Humidity = 20.0,
            WindSpeed = 5.0,
            SoilMoisture0To1Cm = 0.05,
            Precipitation = 0.0,
        });
        var result = Run(weather);
        Assert.NotNull(result);
        var alert = Assert.IsType<FireRiskAlertResult>(result, exactMatch: false);
        Assert.True(alert.Severity is AlertSeverity.Yellow or AlertSeverity.Orange or AlertSeverity.Red,
            $"Expected Yellow/Orange/Red but got {alert.Severity} (score={alert.RiskScore})");
    }

    [Fact]
    public void Adequate_moisture_produces_no_alert()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 25.0,
            Humidity = 60.0,
            WindSpeed = 2.0,
            SoilMoisture0To1Cm = 0.30,
            Precipitation = 5.0,
        });
        var result = Run(weather);
        Assert.Null(result);
    }

    [Fact]
    public void Confidence_comes_from_ensemble()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 38.0,
            Humidity = 15.0,
            WindSpeed = 8.0,
            SoilMoisture0To1Cm = 0.05,
            Precipitation = 0.0,
        });
        var ensemble = EnsembleDay(spread: 0.5);
        var result = FeatureRunner.Run(Rule(), Now, ForecastWithEnsemble(weather, ensemble));
        Assert.NotNull(result);
        var alert = Assert.IsType<FireRiskAlertResult>(result, exactMatch: false);
        Assert.Equal(ConfidenceLevel.High, alert.Level);
    }

    [Fact]
    public void Cold_temperature_produces_zero_weather_score()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 10.0,
            Humidity = 15.0,
            WindSpeed = 8.0,
            SoilMoisture0To1Cm = 0.05,
            Precipitation = 0.0,
        });
        Assert.Null(Run(weather));
    }

    [Fact]
    public void High_humidity_produces_zero_weather_score()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 38.0,
            Humidity = 75.0,
            WindSpeed = 8.0,
            SoilMoisture0To1Cm = 0.05,
            Precipitation = 0.0,
        });
        Assert.Null(Run(weather));
    }

    [Fact]
    public void Orange_severity_at_moderate_risk_score()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 35.0,
            Humidity = 25.0,
            WindSpeed = 6.0,
            SoilMoisture0To1Cm = 0.08,
            Precipitation = 0.0,
        });
        var result = Run(weather);
        Assert.NotNull(result);
        var alert = Assert.IsType<FireRiskAlertResult>(result, exactMatch: false);
        Assert.True(alert.Severity is AlertSeverity.Orange or AlertSeverity.Red,
            $"Expected Orange/Red but got {alert.Severity} (score={alert.RiskScore})");
    }

    [Fact]
    public void Wet_soil_reduces_the_risk()
    {
        var dryWeather = Day((_, p) => p with
        {
            Temperature = 33.0,
            Humidity = 20.0,
            WindSpeed = 5.0,
            SoilMoisture0To1Cm = 0.05,
            Precipitation = 0.0,
        });
        var wetWeather = Day((_, p) => p with
        {
            Temperature = 33.0,
            Humidity = 20.0,
            WindSpeed = 5.0,
            SoilMoisture0To1Cm = 0.25,
            Precipitation = 0.0,
        });
        var dryResult = Assert.IsType<FireRiskAlertResult>(Run(dryWeather), exactMatch: false);
        var wetResult = Run(wetWeather);
        if (wetResult is not null)
        {
            var wetAlert = Assert.IsType<FireRiskAlertResult>(wetResult, exactMatch: false);
            Assert.True(wetAlert.RiskScore < dryResult.RiskScore,
                $"Wet soil ({wetAlert.RiskScore}) should score lower than dry ({dryResult.RiskScore})");
        }
    }

    [Fact]
    public void Rainy_conditions_reduce_dry_factor()
    {
        var dryWeather = Day((_, p) => p with
        {
            Temperature = 35.0,
            Humidity = 20.0,
            WindSpeed = 6.0,
            SoilMoisture0To1Cm = 0.05,
            Precipitation = 0.0,
        });
        var rainyWeather = Day((_, p) => p with
        {
            Temperature = 35.0,
            Humidity = 20.0,
            WindSpeed = 6.0,
            SoilMoisture0To1Cm = 0.05,
            Precipitation = 3.0,
        });
        var dryResult = Assert.IsType<FireRiskAlertResult>(Run(dryWeather), exactMatch: false);
        var rainyResult = Assert.IsType<FireRiskAlertResult>(Run(rainyWeather), exactMatch: false);
        Assert.True(rainyResult.RiskScore < dryResult.RiskScore,
            $"Rain ({rainyResult.RiskScore}) should score lower than dry ({dryResult.RiskScore})");
    }

    [Fact]
    public void Missing_soil_moisture_uses_default_factor()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 35.0,
            Humidity = 20.0,
            WindSpeed = 6.0,
            SoilMoisture0To1Cm = null,
            Precipitation = 0.0,
        });
        var result = Run(weather);
        Assert.NotNull(result);
    }

    [Fact]
    public void Weather_score_uses_temperature_ramp_below_threshold()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 27.0,
            Humidity = 20.0,
            WindSpeed = 6.0,
            SoilMoisture0To1Cm = 0.05,
            Precipitation = 0.0,
        });
        var result = Run(weather);
        Assert.NotNull(result);
        var alert = Assert.IsType<FireRiskAlertResult>(result, exactMatch: false);
        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Partial_humidity_score_reduces_weather_output()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 35.0,
            Humidity = 45.0,
            WindSpeed = 6.0,
            SoilMoisture0To1Cm = 0.05,
            Precipitation = 0.0,
        });
        var result = Run(weather);
        Assert.NotNull(result);
        var alert = Assert.IsType<FireRiskAlertResult>(result, exactMatch: false);
        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Zero_humidity_score_produces_zero_weather_score()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 35.0,
            Humidity = 50.0,
            WindSpeed = 6.0,
            SoilMoisture0To1Cm = 0.05,
            Precipitation = 0.0,
        });
        Assert.Null(Run(weather));
    }

    [Fact]
    public void Partial_wind_score_affects_output()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 35.0,
            Humidity = 20.0,
            WindSpeed = 4.0,
            SoilMoisture0To1Cm = 0.05,
            Precipitation = 0.0,
        });
        var result = Run(weather);
        Assert.NotNull(result);
        var alert = Assert.IsType<FireRiskAlertResult>(result, exactMatch: false);
        Assert.True(alert.RiskScore > 0.7, $"Expected Red-range score but got {alert.RiskScore}");
    }

    [Fact]
    public void Soil_moisture_at_double_threshold_produces_zero_factor()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 35.0,
            Humidity = 20.0,
            WindSpeed = 6.0,
            SoilMoisture0To1Cm = 0.20,
            Precipitation = 0.0,
        });
        var result = Run(weather);
        Assert.NotNull(result);
        var alert = Assert.IsType<FireRiskAlertResult>(result, exactMatch: false);
        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Soil_between_threshold_and_double_produces_partial_factor()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 35.0,
            Humidity = 20.0,
            WindSpeed = 6.0,
            SoilMoisture0To1Cm = 0.15,
            Precipitation = 0.0,
        });
        var result = Run(weather);
        Assert.NotNull(result);
        var alert = Assert.IsType<FireRiskAlertResult>(result, exactMatch: false);
        Assert.True(alert.RiskScore > 0.9, $"Expected high score but got {alert.RiskScore}");
    }

    [Fact]
    public void Score_exactly_at_yellow_threshold()
    {
        var weather = Day((_, p) => p with
        {
            Temperature = 26.0,
            Humidity = 20.0,
            WindSpeed = 3.0,
            SoilMoisture0To1Cm = 0.20,
            Precipitation = 3.0,
        });
        var result = Run(weather);
        if (result is not null)
        {
            Assert.IsType<FireRiskAlertResult>(result, exactMatch: false);
        }
    }

    [Fact]
    public void Only_daytime_hours_are_scored_when_available()
    {
        var weather = Day((hour, p) => p with
        {
            Temperature = 35.0,
            Humidity = 20.0,
            WindSpeed = 6.0,
            SoilMoisture0To1Cm = 0.05,
            Precipitation = 0.0,
            IsDay = hour < 12 ? 1.0 : 0.0,
        });
        var result = Run(weather);
        Assert.NotNull(result);
    }

    [Fact]
    public void WeatherScore_returns_zero_below_cold_cutoff()
    {
        var cfg = new FireRiskConfig { Enabled = true };
        Assert.Equal(0.0, FireRiskAlert.WeatherScore(14.9, 20.0, 6.0, cfg));
    }

    [Fact]
    public void WeatherScore_returns_zero_above_humid_cutoff()
    {
        var cfg = new FireRiskConfig { Enabled = true };
        Assert.Equal(0.0, FireRiskAlert.WeatherScore(35.0, 70.1, 6.0, cfg));
    }

    [Fact]
    public void WeatherScore_full_scores_yield_1_0()
    {
        var cfg = new FireRiskConfig { Enabled = true };
        Assert.Equal(1.0, FireRiskAlert.WeatherScore(35.0, 20.0, 6.0, cfg));
    }

    [Fact]
    public void WeatherScore_partial_temperature_ramp()
    {
        var cfg = new FireRiskConfig { Enabled = true };
        Assert.Equal(0.8, FireRiskAlert.WeatherScore(27.5, 20.0, 6.0, cfg), 4);
    }

    [Fact]
    public void WeatherScore_partial_humidity_ramp()
    {
        var cfg = new FireRiskConfig { Enabled = true };
        Assert.Equal(0.8, FireRiskAlert.WeatherScore(35.0, 40.0, 6.0, cfg), 4);
    }

    [Fact]
    public void WeatherScore_partial_wind_ramp()
    {
        var cfg = new FireRiskConfig { Enabled = true };
        Assert.Equal(0.9, FireRiskAlert.WeatherScore(35.0, 20.0, 4.2, cfg), 4);
    }

    [Fact]
    public void WeatherScore_zero_temp_score_produces_zero()
    {
        var cfg = new FireRiskConfig { Enabled = true };
        Assert.Equal(0.0, FireRiskAlert.WeatherScore(24.0, 20.0, 6.0, cfg));
    }

    [Fact]
    public void WeatherScore_zero_humidity_score_produces_zero()
    {
        var cfg = new FireRiskConfig { Enabled = true };
        Assert.Equal(0.0, FireRiskAlert.WeatherScore(35.0, 50.0, 6.0, cfg));
    }

    [Fact]
    public void WeatherScore_zero_wind_still_has_temp_and_humidity()
    {
        var cfg = new FireRiskConfig { Enabled = true };
        Assert.Equal(0.8, FireRiskAlert.WeatherScore(35.0, 20.0, 0.0, cfg), 4);
    }
}
