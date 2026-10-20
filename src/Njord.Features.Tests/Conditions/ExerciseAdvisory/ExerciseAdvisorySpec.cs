using Microsoft.Extensions.Options;
using Njord.Features.Conditions.ExerciseAdvisory;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions;

public sealed class ExerciseAdvisorySpec
{
    private static ExerciseAdvisory Feature() => new(Options.Create(new ExerciseAdvisoryConfig { Enabled = true }));

    [Fact]
    public void Clean_air_and_mild_weather_are_rated_safe()
    {
        var advice = Assert.IsType<ExerciseAdvisoryResult>(Assert.Single(Run(Feature(), Now, Combined(Day(), CleanAir()))));

        Assert.Multiple(
            () => Assert.Equal("Safe", advice.State),
            () => Assert.Equal("", advice.Data["reasons"]),
            () => Assert.Equal(FeatureData.Time(Now), advice.Data["best_window"]));
    }

    [Fact]
    public void Heavy_pollution_means_avoid_and_names_the_reasons()
    {
        var air = CleanAir(shape: (_, point) => point with { Pm25 = 80.0, Ozone = 200.0 });

        var advice = Assert.IsType<ExerciseAdvisoryResult>(Assert.Single(Run(Feature(), Now, Combined(Day(), air))));

        Assert.Multiple(
            () => Assert.Equal("Avoid", advice.State),
            () => Assert.Equal("High particulate matter (PM2.5),High ozone", advice.Data["reasons"]));
    }

    [Fact]
    public void The_best_hour_is_returned_as_window()
    {
        var air = CleanAir(shape: (hour, point) => hour == 4 ? point : point with { Pm25 = 30.0 });

        var advice = Assert.IsType<ExerciseAdvisoryResult>(Assert.Single(Run(Feature(), Now, Combined(Day(), air))));

        Assert.Equal(FeatureData.Time(Now.AddHours(4)), advice.Data["best_window"]);
    }

    [Fact]
    public void Nothing_is_reported_without_air_quality_data()
    {
        Assert.Empty(Run(Feature(), Now, WeatherOnly(Day())));
    }

    [Fact]
    public void The_feature_is_off_by_default()
    {
        Assert.False(Feature().Descriptor.DefaultEnabled);
    }
}
