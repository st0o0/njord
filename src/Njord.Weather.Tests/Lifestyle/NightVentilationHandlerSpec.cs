using Njord.Messages.Pipeline;
using Njord.Weather.Lifestyle;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Weather.Tests.Lifestyle.LifestyleFixture;

namespace Njord.Weather.Tests.Lifestyle;

public sealed class NightVentilationHandlerSpec
{
    private static readonly NightVentilationHandler Handler = new();

    [Fact]
    public void Handle_advises_opening_windows_overnight_before_a_hot_day()
    {
        var weather = Hourly(48, (hour, p) => hour == 26 ? p with { Temperature = 31.0 } : p);

        var events = Handler.Handle(WeatherOnly(weather), null, Time);

        var advice = Assert.IsType<PipelineEvent.NightVentilationAdvised>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal(Now.AddHours(6), advice.OpenAt),
            () => Assert.Equal(Now.AddHours(21), advice.CloseAt),
            () => Assert.Equal(15.0, advice.LowTemp),
            () => Assert.Equal(31.0, advice.TomorrowPeak));
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
