using Njord.Forecast.Marine;

namespace Njord.Forecast.Tests.Marine;

public sealed class MarineParamsSpec
{
    private static readonly MarineParams[] Flags =
        [.. Enum.GetValues<MarineParams>().Where(p => p != MarineParams.None)];

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
        var union = MarineParams.WaveHeight | MarineParams.WavePeriod;

        Assert.Multiple(
            () => Assert.True(union.HasFlag(MarineParams.WaveHeight)),
            () => Assert.True(union.HasFlag(MarineParams.WavePeriod)),
            () => Assert.Equal(2, Flags.Count(p => union.HasFlag(p))));
    }

    [Fact]
    public void Should_combine_all_flags_without_collision()
    {
        var all = Flags.Aggregate(MarineParams.None, (acc, p) => acc | p);

        Assert.Equal(Flags.Length, Flags.Count(p => all.HasFlag(p)));
    }
}
