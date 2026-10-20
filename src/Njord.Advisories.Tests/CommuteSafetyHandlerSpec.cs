using Njord.Advisories;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Advisories.Tests;

public sealed class CommuteSafetyHandlerSpec
{
    private static readonly CommuteSafetyHandler Handler = new();

    [Fact]
    public void Handle_reports_safe_for_benign_weather()
    {
        var events = Handler.Handle(WeatherOnly(Day()), null, Time);

        var safety = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal("Safe", safety.State),
            () => Assert.Equal("", safety.Data["risks"]));
    }

    [Fact]
    public void Handle_reports_dangerous_for_ice()
    {
        var weather = Uniform(p => p with { Temperature = 0.0, DewPoint = -0.5 });

        var events = Handler.Handle(WeatherOnly(weather), null, Time);

        var safety = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal("Dangerous", safety.State),
            () => Assert.Equal("Ice", safety.Data["risks"]));
    }

    [Fact]
    public void Handle_reports_caution_for_strong_wind_only()
    {
        var weather = Uniform(p => p with { WindSpeed = 20.0 });

        var events = Handler.Handle(WeatherOnly(weather), null, Time);

        var safety = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal("Caution", safety.State),
            () => Assert.Equal("Strong wind", safety.Data["risks"]));
    }

    [Fact]
    public void Handle_ignores_hazards_beyond_the_next_two_hours()
    {
        var weather = Day((hour, p) => hour >= 2 ? p with { Precipitation = 8.0 } : p);

        var events = Handler.Handle(WeatherOnly(weather), null, Time);

        var safety = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Equal("Safe", safety.State);
    }
}
