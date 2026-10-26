using Microsoft.Extensions.Options;
using Njord.Features.Conditions.ExerciseAdvisory;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions;

public sealed class ExerciseAdvisoryTests
{
    private static ExerciseAdvisory Feature() => new(Options.Create(new ExerciseAdvisoryConfig { Enabled = true }));

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
    public void Nothing_is_reported_without_air_quality_data()
    {
        Assert.Null(Run(Feature(), Now, WeatherOnly(Day())));
    }

    [Fact]
    public void The_feature_is_off_by_default()
    {
        Assert.False(Feature().Descriptor.DefaultEnabled);
    }
}
