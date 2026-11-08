using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Forecast;
using Njord.Features.Scores.SleepQuality;
using Njord.Messages.Results.Scores;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Scores.SleepQuality;

public sealed class SleepQualityScoreTests
{
    private static SleepQualityScore Feature(bool pollenSensitive = false) =>
        new(Options.Create(new SleepQualityConfig { Enabled = true, PollenSensitive = pollenSensitive }));

    private static int ScoreFor(
        Func<ForecastPoint, ForecastPoint> weatherChange,
        Func<Njord.Core.AirQuality.AirQualityPoint, Njord.Core.AirQuality.AirQualityPoint>? airChange = null,
        bool pollenSensitive = false)
    {
        var weather = Uniform(weatherChange);
        var air = CleanAir(shape: (_, p) => airChange is null ? p : airChange(p));
        return Assert.IsType<SleepQualityScoreResult>(Run(Feature(pollenSensitive), Now, Combined(weather, air))).Score;
    }

    [Fact]
    public void Calm_cool_clean_nights_score_100() => Assert.Equal(100, ScoreFor(p => p));

    [Theory]
    [InlineData(26.0, 70)]
    [InlineData(23.0, 85)]
    [InlineData(3.0, 90)]
    public void Warm_and_cold_nights_lose_points(double temperature, int expected) => Assert.Equal(expected, ScoreFor(p => p with { Temperature = temperature }));

    [Fact]
    public void Humid_air_and_fine_particles_lose_points() => Assert.Equal(75, ScoreFor(p => p with { Humidity = 85.0 }, a => a with { Pm25 = 30.0 }));

    [Theory]
    [InlineData(false, 95)]
    [InlineData(true, 80)]
    public void High_pollen_costs_more_for_sensitive_sleepers(bool sensitive, int expected) => Assert.Equal(expected, ScoreFor(p => p, a => a with { BirchPollen = 60.0 }, sensitive));

    [Fact]
    public void Strong_gusts_lose_points() => Assert.Equal(90, ScoreFor(p => p with { WindGusts = 12.0 }));

    [Fact]
    public void Thunderstorm_potential_with_rain_loses_points() => Assert.Equal(85, ScoreFor(p => p with { Cape = 1500.0, Precipitation = 1.0 }));

    [Fact]
    public void Stacked_penalties_never_push_the_score_below_zero()
    {
        var score = ScoreFor(
            p => p with { Temperature = 26.0, Humidity = 90.0, WindGusts = 12.0, Cape = 1500.0, Precipitation = 1.0 },
            a => a with { Pm25 = 30.0, BirchPollen = 60.0 },
            pollenSensitive: true);

        Assert.Equal(0, score);
    }

    [Fact]
    public void The_cleanest_evening_stretch_is_reported_as_the_ventilation_window()
    {
        var weather = Day((hour, p) => hour is 4 or 5 ? p with { Precipitation = 1.0 } : p);
        var air = CleanAir(shape: (hour, p) => hour is 4 or 5 ? p with { Pm25 = 30.0 } : p);

        var score = Assert.IsType<SleepQualityScoreResult>(Run(Feature(), Now, Combined(weather, air)));

        Assert.Multiple(
            () => Assert.Equal(Now.AddHours(6), score.BestStart),
            () => Assert.Equal(4, score.BestHours),
            () => Assert.NotEmpty(score.Windows));
    }

    [Fact]
    public void A_forecast_without_night_hours_produces_no_score() => Assert.Null(Run(Feature(), Now, Combined(Hourly(8), CleanAir(8))));

    [Fact]
    public void High_cape_without_rain_does_not_penalize() => Assert.Equal(100, ScoreFor(p => p with { Cape = 1500.0, Precipitation = 0.0 }));

    [Fact]
    public void Pollen_sensitive_reduces_ventilation_window_availability()
    {
        var weather = Day((_, p) => p);
        var air = CleanAir(shape: (_, p) => p with { BirchPollen = 60.0 });

        var sensitive = Assert.IsType<SleepQualityScoreResult>(Run(Feature(pollenSensitive: true), Now, Combined(weather, air)));
        var normal = Assert.IsType<SleepQualityScoreResult>(Run(Feature(pollenSensitive: false), Now, Combined(weather, air)));

        Assert.True(sensitive.BestHours <= normal.BestHours, "Pollen-sensitive should have equal or fewer ventilation hours");
    }

    [Fact]
    public void Hot_and_humid_night_with_all_penalties_scores_low()
    {
        var score = ScoreFor(
            p => p with { Temperature = 26.0, Humidity = 85.0, WindGusts = 12.0 },
            a => a with { Pm25 = 30.0 });
        Assert.True(score <= 35, $"Expected very low score but got {score}");
    }

    [Fact]
    public void Temperature_between_22_and_25_loses_15_points() => Assert.Equal(85, ScoreFor(p => p with { Temperature = 23.5 }));

    [Fact]
    public void Temperature_below_5_loses_10_points() => Assert.Equal(90, ScoreFor(p => p with { Temperature = 3.0 }));

    [Fact]
    public void Temperature_at_22_does_not_lose_points() => Assert.Equal(100, ScoreFor(p => p with { Temperature = 22.0 }));

    [Fact]
    public void Temperature_at_25_loses_15_not_30() => Assert.Equal(85, ScoreFor(p => p with { Temperature = 25.0 }));

    [Fact]
    public void Humidity_at_80_does_not_lose_points() => Assert.Equal(100, ScoreFor(p => p with { Humidity = 80.0 }));

    [Fact]
    public void Pm25_at_25_does_not_lose_points() => Assert.Equal(100, ScoreFor(p => p, a => a with { Pm25 = 25.0 }));

    [Fact]
    public void Gusts_at_11_1_does_not_lose_points() => Assert.Equal(100, ScoreFor(p => p with { WindGusts = 11.1 }));

    [Fact]
    public void Grass_pollen_above_50_counts_as_high() => Assert.Equal(95, ScoreFor(p => p, a => a with { GrassPollen = 55.0 }));

    [Theory]
    [InlineData(22, true)]
    [InlineData(23, true)]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(12, false)]
    [InlineData(21, false)]
    public void IsNight_wraps_around_midnight(int hour, bool expected)
    {
        var cfg = new SleepQualityConfig { NightStartHour = 22, NightEndHour = 6 };
        Assert.Equal(expected, SleepQualityScore.IsNight(cfg, hour));
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(17, true)]
    [InlineData(21, true)]
    [InlineData(22, false)]
    [InlineData(12, false)]
    [InlineData(15, false)]
    public void IsPreSleep_covers_1_to_6_hours_before_night(int hour, bool expected)
    {
        var cfg = new SleepQualityConfig { NightStartHour = 22, NightEndHour = 6 };
        Assert.Equal(expected, SleepQualityScore.IsPreSleep(cfg, hour));
    }

    [Fact]
    public void HighPollenPresent_returns_true_when_birch_above_50() =>
        Assert.True(SleepQualityScore.HighPollenPresent(new AirQualityPoint(DateTimeOffset.UtcNow, BirchPollen: 55.0)));

    [Fact]
    public void HighPollenPresent_returns_false_when_below_50() =>
        Assert.False(SleepQualityScore.HighPollenPresent(new AirQualityPoint(DateTimeOffset.UtcNow, BirchPollen: 40.0, GrassPollen: 30.0)));

    [Fact]
    public void HighPollenPresent_returns_false_for_null() =>
        Assert.False(SleepQualityScore.HighPollenPresent(null));

    [Fact]
    public void NightScore_perfect_conditions_yield_100()
    {
        var cfg = new SleepQualityConfig { Enabled = true };
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Temperature: 18.0, Humidity: 50.0, WindGusts: 5.0, Cape: 0.0, Precipitation: 0.0);
        Assert.Equal(100.0, SleepQualityScore.NightScore(cfg, weather, null));
    }

    [Fact]
    public void NightScore_hot_night_deducts_30()
    {
        var cfg = new SleepQualityConfig { Enabled = true };
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Temperature: 26.0, Humidity: 50.0);
        Assert.Equal(70.0, SleepQualityScore.NightScore(cfg, weather, null));
    }

    [Fact]
    public void NightScore_warm_night_deducts_15()
    {
        var cfg = new SleepQualityConfig { Enabled = true };
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Temperature: 23.0, Humidity: 50.0);
        Assert.Equal(85.0, SleepQualityScore.NightScore(cfg, weather, null));
    }

    [Fact]
    public void NightScore_cold_night_deducts_10()
    {
        var cfg = new SleepQualityConfig { Enabled = true };
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Temperature: 3.0, Humidity: 50.0);
        Assert.Equal(90.0, SleepQualityScore.NightScore(cfg, weather, null));
    }

    [Fact]
    public void NightScore_all_penalties_clamp_to_zero()
    {
        var cfg = new SleepQualityConfig { Enabled = true, PollenSensitive = true };
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Temperature: 26.0, Humidity: 85.0, WindGusts: 12.0, Cape: 1500.0, Precipitation: 1.0);
        var air = new AirQualityPoint(DateTimeOffset.UtcNow, Pm25: 30.0, BirchPollen: 60.0);
        Assert.Equal(0.0, SleepQualityScore.NightScore(cfg, weather, air));
    }

    [Fact]
    public void VentilationScore_perfect_conditions_yield_100()
    {
        var cfg = new SleepQualityConfig { Enabled = true };
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Temperature: 20.0, Humidity: 50.0, WindGusts: 5.0, Precipitation: 0.0);
        Assert.Equal(100.0, SleepQualityScore.VentilationScore(cfg, weather, null));
    }

    [Fact]
    public void VentilationScore_hot_deducts_30()
    {
        var cfg = new SleepQualityConfig { Enabled = true };
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Temperature: 26.0, Humidity: 50.0, WindGusts: 5.0, Precipitation: 0.0);
        Assert.Equal(70.0, SleepQualityScore.VentilationScore(cfg, weather, null));
    }

    [Fact]
    public void VentilationScore_rain_deducts_30()
    {
        var cfg = new SleepQualityConfig { Enabled = true };
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Temperature: 20.0, Humidity: 50.0, WindGusts: 5.0, Precipitation: 0.5);
        Assert.Equal(70.0, SleepQualityScore.VentilationScore(cfg, weather, null));
    }

    [Fact]
    public void VentilationScore_polluted_air_deducts_40()
    {
        var cfg = new SleepQualityConfig { Enabled = true };
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Temperature: 20.0, Humidity: 50.0, WindGusts: 5.0, Precipitation: 0.0);
        var air = new AirQualityPoint(DateTimeOffset.UtcNow, Pm25: 30.0);
        Assert.Equal(60.0, SleepQualityScore.VentilationScore(cfg, weather, air));
    }
}
