using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Conditions.PollenTrend;
using Njord.Forecast.AirQuality;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AirQualityFixture;
using static Njord.Tests.Shared.FeatureRunner;

namespace Njord.Features.Tests.Conditions;

public sealed class PollenTrendSpec
{
    private static PollenTrend Feature() => new(Options.Create(new PollenTrendConfig { Enabled = true }));

    private static LocationForecast With(Func<int, AirQualityPoint, AirQualityPoint> shape) =>
        new(Lucerne, AirQuality: Day(shape));

    [Fact]
    public void Each_pollen_type_is_classified_against_the_previous_cycle()
    {
        var current = With((_, p) => p with { BirchPollen = 30.0, GrassPollen = 10.0, Alder = 10.0 });
        var previous = With((_, p) => p with { BirchPollen = 10.0, GrassPollen = 10.0, Alder = 30.0 });

        var trend = Assert.IsType<PollenTrendResult>(Assert.Single(Run(Feature(), Now, previous, current)));

        Assert.Multiple(
            () => Assert.Equal("rising", trend.Data["trend_birch"]),
            () => Assert.Equal("stable", trend.Data["trend_grass"]),
            () => Assert.Equal("falling", trend.Data["trend_alder"]));
    }

    [Fact]
    public void The_peak_type_and_time_are_reported()
    {
        var current = With((hour, p) => p with { BirchPollen = 5.0, GrassPollen = hour == 7 ? 60.0 : 5.0 });
        var previous = With((_, p) => p with { BirchPollen = 5.0, GrassPollen = 5.0 });

        var trend = Assert.IsType<PollenTrendResult>(Assert.Single(Run(Feature(), Now, previous, current)));

        Assert.Multiple(
            () => Assert.Equal("grass", trend.Direction),
            () => Assert.Equal(FeatureData.Time(Now.AddHours(7)), trend.Data["peak_time"]));
    }

    [Fact]
    public void Nothing_is_reported_without_a_previous_cycle()
    {
        Assert.Empty(Run(Feature(), Now, With((_, p) => p with { BirchPollen = 30.0 })));
    }

    [Fact]
    public void Nothing_is_reported_when_no_pollen_is_present()
    {
        Assert.Empty(Run(Feature(), Now, With((_, p) => p), With((_, p) => p)));
    }

    [Fact]
    public void The_feature_requires_every_pollen_type()
    {
        Assert.Equal(
            new DomainRequirements(AirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
                | AirQualityParams.Alder | AirQualityParams.Mugwort),
            Feature().Descriptor.Requirements);
    }
}
