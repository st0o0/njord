using Microsoft.Extensions.Options;
using Njord.Features.Conditions.VentilationWindow;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions;

public sealed class VentilationWindowSpec
{
    private static VentilationWindow Feature() => new(Options.Create(new VentilationWindowConfig { Enabled = true }));

    [Fact]
    public void The_whole_day_is_advised_when_air_and_weather_stay_good()
    {
        var advice = Assert.IsType<VentilationWindowResult>(Assert.Single(Run(Feature(), Now, Combined(Day(), CleanAir()))));

        Assert.Multiple(
            () => Assert.Equal(Now, advice.BestStart),
            () => Assert.Equal(Now.AddHours(24), advice.BestEnd));
    }

    [Fact]
    public void The_longest_contiguous_window_is_picked()
    {
        var air = CleanAir(shape: (hour, point) => hour is 3 or 4 ? point with { Pm25 = 40.0 } : point);

        var advice = Assert.IsType<VentilationWindowResult>(Assert.Single(Run(Feature(), Now, Combined(Day(), air))));

        Assert.Multiple(
            () => Assert.Equal(Now.AddHours(5), advice.BestStart),
            () => Assert.Equal(Now.AddHours(24), advice.BestEnd));
    }

    [Fact]
    public void Nothing_is_reported_when_the_air_is_polluted()
    {
        var air = CleanAir(shape: (_, point) => point with { Pm25 = 40.0 });

        Assert.Empty(Run(Feature(), Now, Combined(Day(), air)));
    }

    [Fact]
    public void Nothing_is_reported_without_air_quality_data()
    {
        Assert.Empty(Run(Feature(), Now, WeatherOnly(Day())));
    }
}
