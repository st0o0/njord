using Njord.Advisories;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Advisories.Tests;

public sealed class NightVentilationHandlerSpec
{
    private static readonly NightVentilationHandler Handler = new();

    [Fact]
    public void Handle_advises_opening_windows_overnight_before_a_hot_day()
    {
        var weather = Hourly(48, (hour, p) => hour == 26 ? p with { Temperature = 31.0 } : p);

        var events = Handler.Handle(WeatherOnly(weather), null, Time);

        var advice = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal("open", advice.State),
            () => Assert.Equal(FeatureData.Time(Now.AddHours(6)), advice.Data["open_at"]),
            () => Assert.Equal(FeatureData.Time(Now.AddHours(21)), advice.Data["close_at"]),
            () => Assert.Equal("15", advice.Data["low_temp"]),
            () => Assert.Equal("31", advice.Data["tomorrow_peak"]));
    }

    [Fact]
    public void Handle_returns_nothing_when_tomorrow_stays_mild()
    {
        Assert.Empty(Handler.Handle(WeatherOnly(Hourly(48)), null, Time));
    }

    [Fact]
    public void Handle_returns_nothing_when_the_night_stays_warm()
    {
        var weather = Hourly(48, (hour, p) => p with { Temperature = hour == 26 ? 31.0 : 22.0 });

        Assert.Empty(Handler.Handle(WeatherOnly(weather), null, Time));
    }
}
