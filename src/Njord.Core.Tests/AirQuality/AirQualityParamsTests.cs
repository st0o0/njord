using Njord.Core.AirQuality;

namespace Njord.Core.Tests.AirQuality;

public sealed class AirQualityParamsTests
{
    private static readonly AirQualityParams[] _flags =
        [.. Enum.GetValues<AirQualityParams>().Where(p => p != AirQualityParams.None)];

    [Fact]
    public void Should_give_every_flag_exactly_one_distinct_bit()
    {
        Assert.Multiple(
            () => Assert.NotEmpty(_flags),
            () => Assert.All(_flags, p => Assert.True(long.IsPow2(Convert.ToInt64(p)))),
            () => Assert.Equal(_flags.Length, _flags.Distinct().Count()));
    }

    [Fact]
    public void Should_contain_both_members_and_nothing_else_when_two_flags_are_combined()
    {
        var union = AirQualityParams.Pm25 | AirQualityParams.Pm10;

        Assert.Multiple(
            () => Assert.True(union.HasFlag(AirQualityParams.Pm25)),
            () => Assert.True(union.HasFlag(AirQualityParams.Pm10)),
            () => Assert.Equal(2, _flags.Count(p => union.HasFlag(p))));
    }

    [Fact]
    public void Should_combine_all_flags_without_collision()
    {
        var all = _flags.Aggregate(AirQualityParams.None, (acc, p) => acc | p);

        Assert.Equal(_flags.Length, _flags.Count(p => all.HasFlag(p)));
    }
}
