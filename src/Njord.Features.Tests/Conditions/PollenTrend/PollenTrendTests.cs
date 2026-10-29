using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Features.Conditions.PollenTrend;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AirQualityFixture;
using static Njord.Tests.Shared.FeatureRunner;

namespace Njord.Features.Tests.Conditions.PollenTrend;

public sealed class PollenTrendTests
{
    private static Features.Conditions.PollenTrend.PollenTrend Feature() => new(Options.Create(new PollenTrendConfig { Enabled = true }));

    private static LocationForecast With(Func<int, AirQualityPoint, AirQualityPoint> shape) =>
        new(Lucerne, AirQuality: Day(shape));

    [Fact]
    public void Each_pollen_type_is_classified_against_the_previous_cycle()
    {
        var current = With((_, p) => p with { BirchPollen = 30.0, GrassPollen = 10.0, Alder = 10.0 });
        var previous = With((_, p) => p with { BirchPollen = 10.0, GrassPollen = 10.0, Alder = 30.0 });

        var trend = Assert.IsType<PollenTrendResult>(Run(Feature(), Now, previous, current));

        Assert.Multiple(
            () => Assert.Equal(PollenTrendDirection.Rising, trend.Birch),
            () => Assert.Equal(PollenTrendDirection.Stable, trend.Grass),
            () => Assert.Equal(PollenTrendDirection.Falling, trend.Alder));
    }

    [Fact]
    public void The_peak_type_and_time_are_reported()
    {
        var current = With((hour, p) => p with { BirchPollen = 5.0, GrassPollen = hour == 7 ? 60.0 : 5.0 });
        var previous = With((_, p) => p with { BirchPollen = 5.0, GrassPollen = 5.0 });

        var trend = Assert.IsType<PollenTrendResult>(Run(Feature(), Now, previous, current));

        Assert.Multiple(
            () => Assert.Equal(PollenType.Grass, trend.PeakType),
            () => Assert.Equal(Now.AddHours(7), trend.PeakTime));
    }

    [Fact]
    public void Nothing_is_reported_without_a_previous_cycle()
    {
        Assert.Null(Run(Feature(), Now, With((_, p) => p with { BirchPollen = 30.0 })));
    }

    [Fact]
    public void Nothing_is_reported_when_no_pollen_is_present()
    {
        Assert.Null(Run(Feature(), Now, With((_, p) => p), With((_, p) => p)));
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
