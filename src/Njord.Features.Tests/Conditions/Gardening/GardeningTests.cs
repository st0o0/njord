using Microsoft.Extensions.Options;
using Njord.Features.Conditions.Gardening;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions;

public sealed class GardeningTests
{
    private static Gardening Feature() => new(Options.Create(new GardeningConfig { Enabled = true }));

    [Fact]
    public void Frost_is_flagged_and_blocks_planting()
    {
        var forecast = WeatherOnly(Day((hour, point) => hour == 5 ? point with { Temperature = -1.0 } : point));

        var advice = Assert.IsType<GardeningResult>(Run(Feature(), Now, forecast));

        Assert.Multiple(
            () => Assert.True(advice.FrostRisk),
            () => Assert.False(advice.PlantingSafe),
            () => Assert.Equal(GardeningAdvice.Hold, advice.Advice));
    }

    [Fact]
    public void A_mild_dry_day_calls_for_planting_and_watering()
    {
        var advice = Assert.IsType<GardeningResult>(Run(Feature(), Now, WeatherOnly(Day())));

        Assert.Multiple(
            () => Assert.False(advice.FrostRisk),
            () => Assert.True(advice.PlantingSafe),
            () => Assert.True(advice.WateringNeeded),
            () => Assert.Equal(GardeningAdvice.Plant, advice.Advice));
    }

    [Fact]
    public void Expected_rain_skips_watering()
    {
        var forecast = WeatherOnly(Day((hour, point) => hour == 2 ? point with { Precipitation = 5.0 } : point));

        var advice = Assert.IsType<GardeningResult>(Run(Feature(), Now, forecast));

        Assert.False(advice.WateringNeeded);
    }
}
