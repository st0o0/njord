using Njord.Messages.Pipeline;
using Njord.Weather.Lifestyle;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Weather.Tests.Lifestyle.LifestyleFixture;

namespace Njord.Weather.Tests.Lifestyle;

public sealed class GardeningHandlerSpec
{
    private static readonly GardeningHandler Handler = new();

    [Fact]
    public void Handle_flags_frost_and_blocks_planting()
    {
        var forecast = WeatherOnly(Day((hour, point) => hour == 5 ? point with { Temperature = -1.0 } : point));

        var advice = Assert.IsType<PipelineEvent.GardeningAdvised>(Assert.Single(Handler.Handle(forecast, null, Time)));

        Assert.Multiple(
            () => Assert.True(advice.FrostRisk),
            () => Assert.False(advice.PlantingSafe));
    }

    [Fact]
    public void Handle_advises_planting_and_watering_on_a_mild_dry_day()
    {
        var advice = Assert.IsType<PipelineEvent.GardeningAdvised>(Assert.Single(Handler.Handle(WeatherOnly(Day()), null, Time)));

        Assert.Multiple(
            () => Assert.False(advice.FrostRisk),
            () => Assert.True(advice.PlantingSafe),
            () => Assert.True(advice.WateringNeeded));
    }

    [Fact]
    public void Handle_skips_watering_when_rain_is_expected()
    {
        var forecast = WeatherOnly(Day((hour, point) => hour == 2 ? point with { Precipitation = 5.0 } : point));

        var advice = Assert.IsType<PipelineEvent.GardeningAdvised>(Assert.Single(Handler.Handle(forecast, null, Time)));

        Assert.False(advice.WateringNeeded);
    }
}
