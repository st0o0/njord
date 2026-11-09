using Njord.Core.Marine;

namespace Njord.Core.Tests.Marine;

public sealed class MarineParamsTests
{
    private static readonly MarineParams[] _flags =
        [.. Enum.GetValues<MarineParams>().Where(p => p != MarineParams.None)];

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
        var union = MarineParams.WaveHeight | MarineParams.WavePeriod;

        Assert.Multiple(
            () => Assert.True(union.HasFlag(MarineParams.WaveHeight)),
            () => Assert.True(union.HasFlag(MarineParams.WavePeriod)),
            () => Assert.Equal(2, _flags.Count(p => union.HasFlag(p))));
    }

    [Fact]
    public void Should_combine_all_flags_without_collision()
    {
        var all = _flags.Aggregate(MarineParams.None, (acc, p) => acc | p);

        Assert.Equal(_flags.Length, _flags.Count(p => all.HasFlag(p)));
    }
}
