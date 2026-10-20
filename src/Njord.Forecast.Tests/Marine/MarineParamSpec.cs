using Njord.Forecast.Marine;

namespace Njord.Forecast.Tests.Marine;

public sealed class MarineParamSpec
{
    private static readonly MarineParam[] Flags =
        [.. Enum.GetValues<MarineParam>().Where(p => p != MarineParam.None)];

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
        var union = MarineParam.WaveHeight | MarineParam.WavePeriod;

        Assert.Multiple(
            () => Assert.True(union.HasFlag(MarineParam.WaveHeight)),
            () => Assert.True(union.HasFlag(MarineParam.WavePeriod)),
            () => Assert.Equal(2, Flags.Count(p => union.HasFlag(p))));
    }

    [Fact]
    public void Should_combine_all_flags_without_collision()
    {
        var all = Flags.Aggregate(MarineParam.None, (acc, p) => acc | p);

        Assert.Equal(Flags.Length, Flags.Count(p => all.HasFlag(p)));
    }
}
