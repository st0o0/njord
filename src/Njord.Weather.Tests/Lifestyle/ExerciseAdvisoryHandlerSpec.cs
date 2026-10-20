using Njord.Messages.Pipeline;
using Njord.Weather.Lifestyle;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Weather.Tests.Lifestyle.LifestyleFixture;

namespace Njord.Weather.Tests.Lifestyle;

public sealed class ExerciseAdvisoryHandlerSpec
{
    private static readonly ExerciseAdvisoryHandler Handler = new();

    [Fact]
    public void Handle_rates_clean_air_and_mild_weather_as_safe()
    {
        var events = Handler.Handle(Combined(Day(), CleanAir()), null, Time);

        var advice = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal("Safe", advice.State),
            () => Assert.Equal("", advice.Data["reasons"]),
            () => Assert.Equal(FeatureData.Time(Now), advice.Data["best_window"]));
    }

    [Fact]
    public void Handle_advises_to_avoid_exercise_in_heavy_pollution_and_names_the_reasons()
    {
        var air = CleanAir(shape: (_, point) => point with { Pm25 = 80.0, Ozone = 200.0 });

        var events = Handler.Handle(Combined(Day(), air), null, Time);

        var advice = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal("Avoid", advice.State),
            () => Assert.Equal("High particulate matter (PM2.5),High ozone", advice.Data["reasons"]));
    }

    [Fact]
    public void Handle_returns_the_best_hour_as_window()
    {
        var air = CleanAir(shape: (hour, point) => hour == 4 ? point : point with { Pm25 = 30.0 });

        var events = Handler.Handle(Combined(Day(), air), null, Time);

        var advice = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Equal(FeatureData.Time(Now.AddHours(4)), advice.Data["best_window"]);
    }

    [Fact]
    public void Handle_returns_nothing_without_air_quality_data()
    {
        Assert.Empty(Handler.Handle(WeatherOnly(Day()), null, Time));
    }
}
