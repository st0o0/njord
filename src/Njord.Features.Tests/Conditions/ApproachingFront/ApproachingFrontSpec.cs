using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Conditions.ApproachingFront;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions;

public sealed class ApproachingFrontSpec
{
    private static ApproachingFront Feature() => new(Options.Create(new ApproachingFrontConfig { Enabled = true }));

    private static LocationForecast Falling(double directionAfter) =>
        WeatherOnly(Hourly(30, (hour, point) => point with
        {
            Pressure = 1015.0 - Math.Clamp(hour - 2, 0, 6) * 1.0,
            Temperature = 18.0 - Math.Clamp(hour - 2, 0, 6) * 0.5,
            WindDirection = hour >= 8 ? directionAfter : 180.0,
        }));

    [Fact]
    public void A_pressure_drop_with_a_wind_shift_is_reported()
    {
        var front = Assert.IsType<ApproachingFrontResult>(
            Assert.Single(Run(Feature(), Now, WeatherOnly(Day()), Falling(315.0))));

        Assert.Multiple(
            () => Assert.Equal("approaching", front.State),
            () => Assert.Equal("6", front.Data["pressure_drop"]),
            () => Assert.Equal("3", front.Data["temp_drop"]),
            () => Assert.Equal("2", front.Data["estimated_in_hours"]));
    }

    [Fact]
    public void A_pressure_drop_without_a_wind_shift_is_ignored()
    {
        Assert.Empty(Run(Feature(), Now, WeatherOnly(Day()), Falling(190.0)));
    }

    [Fact]
    public void Nothing_is_reported_without_a_previous_cycle()
    {
        Assert.Empty(Run(Feature(), Now, Falling(315.0)));
    }
}
