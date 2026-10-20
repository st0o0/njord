using Microsoft.Extensions.Options;
using Njord.Features.Conditions.NightVentilation;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions;

public sealed class NightVentilationSpec
{
    private static NightVentilation Feature() => new(Options.Create(new NightVentilationConfig { Enabled = true }));

    [Fact]
    public void Opening_windows_overnight_is_advised_before_a_hot_day()
    {
        var weather = Hourly(48, (hour, p) => hour == 26 ? p with { Temperature = 31.0 } : p);

        var advice = Assert.IsType<NightVentilationResult>(Assert.Single(Run(Feature(), Now, WeatherOnly(weather))));

        Assert.Multiple(
            () => Assert.Equal("open", advice.State),
            () => Assert.Equal(FeatureData.Time(Now.AddHours(6)), advice.Data["open_at"]),
            () => Assert.Equal(FeatureData.Time(Now.AddHours(21)), advice.Data["close_at"]),
            () => Assert.Equal("15", advice.Data["low_temp"]),
            () => Assert.Equal("31", advice.Data["tomorrow_peak"]));
    }

    [Fact]
    public void Nothing_is_reported_when_tomorrow_stays_mild()
    {
        Assert.Empty(Run(Feature(), Now, WeatherOnly(Hourly(48))));
    }

    [Fact]
    public void Nothing_is_reported_when_the_night_stays_warm()
    {
        var weather = Hourly(48, (hour, p) => p with { Temperature = hour == 26 ? 31.0 : 22.0 });

        Assert.Empty(Run(Feature(), Now, WeatherOnly(weather)));
    }
}
