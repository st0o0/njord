using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using Njord.Weather.Lifestyle;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Weather.Tests.Lifestyle.LifestyleFixture;

namespace Njord.Weather.Tests.Lifestyle;

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
        var front = Assert.IsType<PipelineEvent.FrontApproaching>(
            Assert.Single(Handler.Handle(Falling(315.0), WeatherOnly(Day()), Time)));

        Assert.Multiple(
            () => Assert.Equal(6.0, front.PressureDrop),
            () => Assert.Equal(3.0, front.TempDrop),
            () => Assert.Equal(TimeSpan.FromHours(2), front.EstimatedIn));
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
