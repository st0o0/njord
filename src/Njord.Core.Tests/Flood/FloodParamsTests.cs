using Njord.Core.Flood;

namespace Njord.Core.Tests.Flood;

public sealed class FloodParamsTests
{
    private static readonly FloodParams[] Flags =
        [.. Enum.GetValues<FloodParams>().Where(p => p != FloodParams.None)];

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
        var union = FloodParams.Discharge | FloodParams.DischargeMean;

        Assert.Multiple(
            () => Assert.True(union.HasFlag(FloodParams.Discharge)),
            () => Assert.True(union.HasFlag(FloodParams.DischargeMean)),
            () => Assert.Equal(2, Flags.Count(p => union.HasFlag(p))));
    }

    [Fact]
    public void Should_combine_all_flags_without_collision()
    {
        var all = Flags.Aggregate(FloodParams.None, (acc, p) => acc | p);

        Assert.Equal(Flags.Length, Flags.Count(p => all.HasFlag(p)));
    }
}
