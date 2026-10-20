using Njord.Core.Domain;
using Njord.Insights;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Insights.Tests;

public sealed class ApproachingFrontHandlerSpec
{
    private static readonly ApproachingFrontHandler Handler = new();

    private static LocationForecast Falling(double directionAfter) =>
        WeatherOnly(Hourly(30, (hour, point) => point with
        {
            Pressure = 1015.0 - Math.Clamp(hour - 2, 0, 6) * 1.0,
            Temperature = 18.0 - Math.Clamp(hour - 2, 0, 6) * 0.5,
            WindDirection = hour >= 8 ? directionAfter : 180.0,
        }));

    [Fact]
    public void Handle_reports_a_pressure_drop_with_a_wind_shift()
    {
        var front = Assert.IsType<ConditionResult>(
            Assert.Single(Handler.Handle(Falling(315.0), WeatherOnly(Day()), Time)));

        Assert.Multiple(
            () => Assert.Equal("frontapproaching", front.WireKey),
            () => Assert.Equal("approaching", front.State),
            () => Assert.Equal("6", front.Data["pressure_drop"]),
            () => Assert.Equal("3", front.Data["temp_drop"]),
            () => Assert.Equal("2", front.Data["estimated_in_hours"]));
    }

    [Fact]
    public void Handle_ignores_a_pressure_drop_without_a_wind_shift()
    {
        Assert.Empty(Handler.Handle(Falling(190.0), WeatherOnly(Day()), Time));
    }

    [Fact]
    public void Handle_returns_nothing_without_a_previous_cycle()
    {
        Assert.Empty(Handler.Handle(Falling(315.0), null, Time));
    }
}
