using Microsoft.Extensions.Options;
using Njord.Core.Forecast;
using Njord.Features.Scores.SleepQuality;
using Njord.Messages.Pipeline;
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
    public void Calm_cool_clean_nights_score_100()
    {
        Assert.Equal(100, ScoreFor(p => p));
    }

    [Theory]
    [InlineData(26.0, 70)]
    [InlineData(23.0, 85)]
    [InlineData(3.0, 90)]
    public void Warm_and_cold_nights_lose_points(double temperature, int expected)
    {
        Assert.Equal(expected, ScoreFor(p => p with { Temperature = temperature }));
    }

    [Fact]
    public void Humid_air_and_fine_particles_lose_points()
    {
        Assert.Equal(75, ScoreFor(p => p with { Humidity = 85.0 }, a => a with { Pm25 = 30.0 }));
    }

    [Theory]
    [InlineData(false, 95)]
    [InlineData(true, 80)]
    public void High_pollen_costs_more_for_sensitive_sleepers(bool sensitive, int expected)
    {
        Assert.Equal(expected, ScoreFor(p => p, a => a with { BirchPollen = 60.0 }, sensitive));
    }

    [Fact]
    public void Strong_gusts_lose_points()
    {
        Assert.Equal(90, ScoreFor(p => p with { WindGusts = 12.0 }));
    }

    [Fact]
    public void Thunderstorm_potential_with_rain_loses_points()
    {
        Assert.Equal(85, ScoreFor(p => p with { Cape = 1500.0, Precipitation = 1.0 }));
    }

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
    public void A_forecast_without_night_hours_produces_no_score()
    {
        Assert.Null(Run(Feature(), Now, Combined(Hourly(8), CleanAir(8))));
    }
}
