using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Forecast;
using Njord.Features.Conditions.ExerciseAdvisory;
using Njord.Messages.Results.Conditions;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.ExerciseAdvisory;

public sealed class ExerciseAdvisoryTests
{
    private static Features.Conditions.ExerciseAdvisory.ExerciseAdvisory Feature() => new(Options.Create(new ExerciseAdvisoryConfig { Enabled = true }));

    [Fact]
    public void Clean_air_and_mild_weather_are_rated_safe()
    {
        var advice = Assert.IsType<ExerciseAdvisoryResult>(Run(Feature(), Now, Combined(Day(), CleanAir())));

        Assert.Multiple(
            () => Assert.Equal(ExerciseLevel.Safe, advice.Level),
            () => Assert.Empty(advice.Reasons),
            () => Assert.Equal(Now, advice.BestWindow));
    }

    [Fact]
    public void Heavy_pollution_means_avoid_and_names_the_reasons()
    {
        var air = CleanAir(shape: (_, point) => point with { Pm25 = 80.0, Ozone = 200.0 });

        var advice = Assert.IsType<ExerciseAdvisoryResult>(Run(Feature(), Now, Combined(Day(), air)));

        Assert.Multiple(
            () => Assert.Equal(ExerciseLevel.Avoid, advice.Level),
            () => Assert.Equal([ExerciseFactor.Pm25, ExerciseFactor.Ozone], advice.Reasons));
    }

    [Fact]
    public void The_best_hour_is_returned_as_window()
    {
        var air = CleanAir(shape: (hour, point) => hour == 4 ? point : point with { Pm25 = 30.0 });

        var advice = Assert.IsType<ExerciseAdvisoryResult>(Run(Feature(), Now, Combined(Day(), air)));

        Assert.Equal(Now.AddHours(4), advice.BestWindow);
    }

    [Fact]
    public void Nothing_is_reported_without_air_quality_data() => Assert.Null(Run(Feature(), Now, ForecastOnly(Day())));

    [Fact]
    public void High_temperature_causes_caution_with_temperature_reason()
    {
        var weather = Uniform(p => p with { Temperature = 34.0, Humidity = 50.0 });
        var air = CleanAir(shape: (_, p) => p with { Pm25 = 5.0, Ozone = 40.0 });
        var advice = Assert.IsType<ExerciseAdvisoryResult>(Run(Feature(), Now, Combined(weather, air)));
        Assert.Contains(ExerciseFactor.Temperature, advice.Reasons);
    }

    [Fact]
    public void High_humidity_contributes_to_reasons()
    {
        var weather = Uniform(p => p with { Temperature = 15.0, Humidity = 95.0 });
        var air = CleanAir(shape: (_, p) => p with { Pm25 = 5.0, Ozone = 40.0 });
        var advice = Assert.IsType<ExerciseAdvisoryResult>(Run(Feature(), Now, Combined(weather, air)));
        Assert.Contains(ExerciseFactor.Humidity, advice.Reasons);
    }

    [Fact]
    public void High_uv_contributes_to_reasons()
    {
        var weather = Uniform(p => p with { Temperature = 15.0, Humidity = 50.0, UvIndex = 10.0 });
        var air = CleanAir(shape: (_, p) => p with { Pm25 = 5.0, Ozone = 40.0 });
        var advice = Assert.IsType<ExerciseAdvisoryResult>(Run(Feature(), Now, Combined(weather, air)));
        Assert.Contains(ExerciseFactor.Uv, advice.Reasons);
    }

    [Fact]
    public void Moderate_pollution_results_in_caution()
    {
        var air = CleanAir(shape: (_, p) => p with { Pm25 = 40.0, Ozone = 130.0 });
        var advice = Assert.IsType<ExerciseAdvisoryResult>(Run(Feature(), Now, Combined(Day(), air)));
        Assert.Equal(ExerciseLevel.Caution, advice.Level);
    }

    [Fact]
    public void Components_returns_null_without_pm25()
    {
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Temperature: 20.0, Humidity: 50.0);
        var air = new AirQualityPoint(DateTimeOffset.UtcNow, Ozone: 50.0);
        Assert.Null(Features.Conditions.ExerciseAdvisory.ExerciseAdvisory.Components(weather, air));
    }

    [Fact]
    public void Components_returns_null_without_temperature()
    {
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Humidity: 50.0);
        var air = new AirQualityPoint(DateTimeOffset.UtcNow, Pm25: 5.0, Ozone: 50.0);
        Assert.Null(Features.Conditions.ExerciseAdvisory.ExerciseAdvisory.Components(weather, air));
    }

    [Fact]
    public void Components_computes_total_from_weighted_factors()
    {
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Temperature: 15.0, Humidity: 50.0);
        var air = new AirQualityPoint(DateTimeOffset.UtcNow, Pm25: 15.0, Ozone: 100.0);
        var hour = Features.Conditions.ExerciseAdvisory.ExerciseAdvisory.Components(weather, air);
        Assert.NotNull(hour);
        Assert.Equal(100.0, hour.Total, 1);
    }

    [Fact]
    public void Components_high_pm25_reduces_pm25_factor()
    {
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Temperature: 15.0, Humidity: 50.0);
        var air = new AirQualityPoint(DateTimeOffset.UtcNow, Pm25: 35.0, Ozone: 100.0);
        var hour = Features.Conditions.ExerciseAdvisory.ExerciseAdvisory.Components(weather, air);
        Assert.NotNull(hour);
        Assert.Equal(0.5, hour.Pm25, 2);
    }

    [Fact]
    public void Components_high_uv_reduces_uv_factor()
    {
        var weather = new ForecastPoint(DateTimeOffset.UtcNow, Temperature: 15.0, Humidity: 50.0, UvIndex: 8.0);
        var air = new AirQualityPoint(DateTimeOffset.UtcNow, Pm25: 15.0, Ozone: 100.0);
        var hour = Features.Conditions.ExerciseAdvisory.ExerciseAdvisory.Components(weather, air);
        Assert.NotNull(hour);
        Assert.Equal(0.5, hour.Uv, 2);
    }
}
