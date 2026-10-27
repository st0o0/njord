using Microsoft.Extensions.Options;
using Njord.Features.Conditions.NightVentilation;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions;

public sealed class NightVentilationTests
{
    private static NightVentilation Feature() => new(Options.Create(new NightVentilationConfig { Enabled = true }));

    [Fact]
    public void Opening_windows_overnight_is_advised_before_a_hot_day()
    {
        var weather = Hourly(48, (hour, p) => hour == 26 ? p with { Temperature = 31.0 } : p);

        var advice = Assert.IsType<NightVentilationResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.Multiple(
            () => Assert.Equal(Now.AddHours(6), advice.OpenAt),
            () => Assert.Equal(Now.AddHours(21), advice.CloseAt),
            () => Assert.Equal(15.0, advice.LowTemperature),
            () => Assert.Equal(31.0, advice.TomorrowPeak));
    }

    [Fact]
    public void Nothing_is_reported_when_tomorrow_stays_mild()
    {
        Assert.Null(Run(Feature(), Now, ForecastOnly(Hourly(48))));
    }

    [Fact]
    public void Nothing_is_reported_when_the_night_stays_warm()
    {
        var weather = Hourly(48, (hour, p) => p with { Temperature = hour == 26 ? 31.0 : 22.0 });

        Assert.Null(Run(Feature(), Now, ForecastOnly(weather)));
    }
}
