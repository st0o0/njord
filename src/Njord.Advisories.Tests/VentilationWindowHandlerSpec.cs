using Njord.Advisories;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Advisories.Tests;

public sealed class VentilationWindowHandlerSpec
{
    private static readonly VentilationWindowHandler Handler = new();

    [Fact]
    public void Handle_advises_the_whole_day_when_air_and_weather_stay_good()
    {
        var events = Handler.Handle(Combined(Day(), CleanAir()), null, Time);

        var advice = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal("open", advice.State),
            () => Assert.Equal(FeatureData.Time(Now), advice.Data["best_start"]),
            () => Assert.Equal(FeatureData.Time(Now.AddHours(24)), advice.Data["best_end"]),
            () => Assert.Equal("Clean air and mild dry weather for 24 h", advice.Data["reason"]));
    }

    [Fact]
    public void Handle_picks_the_longest_contiguous_window()
    {
        var air = CleanAir(shape: (hour, point) => hour is 3 or 4 ? point with { Pm25 = 40.0 } : point);

        var events = Handler.Handle(Combined(Day(), air), null, Time);

        var advice = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal(FeatureData.Time(Now.AddHours(5)), advice.Data["best_start"]),
            () => Assert.Equal(FeatureData.Time(Now.AddHours(24)), advice.Data["best_end"]));
    }

    [Fact]
    public void Handle_returns_nothing_when_the_air_is_polluted()
    {
        var air = CleanAir(shape: (_, point) => point with { Pm25 = 40.0 });

        Assert.Empty(Handler.Handle(Combined(Day(), air), null, Time));
    }

    [Fact]
    public void Handle_returns_nothing_without_air_quality_data()
    {
        Assert.Empty(Handler.Handle(WeatherOnly(Day()), null, Time));
    }
}
