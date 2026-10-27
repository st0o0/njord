using Njord.Core.AirQuality;

namespace Njord.Core.Tests.AirQuality;

public sealed class AirQualityParamsTests
{
    private static readonly AirQualityParams[] Flags =
        [.. Enum.GetValues<AirQualityParams>().Where(p => p != AirQualityParams.None)];

    [Fact]
    public void Should_give_every_flag_exactly_one_distinct_bit()
    {
        Assert.Multiple(
            () => Assert.NotEmpty(Flags),
            () => Assert.All(Flags, p => Assert.True(long.IsPow2(Convert.ToInt64(p)))),
            () => Assert.Equal(Flags.Length, Flags.Distinct().Count()));
    }

    [Fact]
    public void Should_contain_both_members_and_nothing_else_when_two_flags_are_combined()
    {
        var union = AirQualityParams.Pm25 | AirQualityParams.Pm10;

        Assert.Multiple(
            () => Assert.True(union.HasFlag(AirQualityParams.Pm25)),
            () => Assert.True(union.HasFlag(AirQualityParams.Pm10)),
            () => Assert.Equal(2, Flags.Count(p => union.HasFlag(p))));
    }

    [Fact]
    public void Should_combine_all_flags_without_collision()
    {
        var all = Flags.Aggregate(AirQualityParams.None, (acc, p) => acc | p);

        Assert.Equal(Flags.Length, Flags.Count(p => all.HasFlag(p)));
    }
}
