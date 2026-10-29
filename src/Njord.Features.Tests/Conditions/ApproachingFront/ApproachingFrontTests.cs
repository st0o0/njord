using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Conditions.ApproachingFront;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.ApproachingFront;

public sealed class ApproachingFrontTests
{
    private static Features.Conditions.ApproachingFront.ApproachingFront Feature() => new(Options.Create(new ApproachingFrontConfig { Enabled = true }));

    private static LocationForecast Falling(double directionAfter) =>
        ForecastOnly(Hourly(30, (hour, point) => point with
        {
            Pressure = 1015.0 - Math.Clamp(hour - 2, 0, 6) * 1.0,
            Temperature = 18.0 - Math.Clamp(hour - 2, 0, 6) * 0.5,
            WindDirection = hour >= 8 ? directionAfter : 180.0,
        }));

    [Fact]
    public void A_pressure_drop_with_a_wind_shift_is_reported()
    {
        var front = Assert.IsType<ApproachingFrontResult>(
            Run(Feature(), Now, ForecastOnly(Day()), Falling(315.0)));

        Assert.Multiple(
            () => Assert.Equal(6.0, front.PressureDrop),
            () => Assert.Equal(3.0, front.TemperatureDrop),
            () => Assert.Equal(2.0, front.EstimatedInHours));
    }

    [Fact]
    public void A_pressure_drop_without_a_wind_shift_is_ignored()
    {
        Assert.Null(Run(Feature(), Now, ForecastOnly(Day()), Falling(190.0)));
    }

    [Fact]
    public void Nothing_is_reported_without_a_previous_cycle()
    {
        Assert.Null(Run(Feature(), Now, Falling(315.0)));
    }
}
