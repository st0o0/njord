using Njord.Core.Flood;

namespace Njord.Core.Tests.Flood;

public sealed class FloodParamsTests
{
    private static readonly FloodParams[] _flags =
        [.. Enum.GetValues<FloodParams>().Where(p => p != FloodParams.None)];

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
        var union = FloodParams.Discharge | FloodParams.DischargeMean;

        Assert.Multiple(
            () => Assert.True(union.HasFlag(FloodParams.Discharge)),
            () => Assert.True(union.HasFlag(FloodParams.DischargeMean)),
            () => Assert.Equal(2, _flags.Count(p => union.HasFlag(p))));
    }

    [Fact]
    public void Should_combine_all_flags_without_collision()
    {
        var all = _flags.Aggregate(FloodParams.None, (acc, p) => acc | p);

        Assert.Equal(_flags.Length, _flags.Count(p => all.HasFlag(p)));
    }
}
