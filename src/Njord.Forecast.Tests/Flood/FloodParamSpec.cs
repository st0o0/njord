using Njord.Forecast.Flood;

namespace Njord.Forecast.Tests.Flood;

public sealed class FloodParamSpec
{
    private static readonly FloodParam[] Flags =
        [.. Enum.GetValues<FloodParam>().Where(p => p != FloodParam.None)];

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
        var union = FloodParam.Discharge | FloodParam.DischargeMean;

        Assert.Multiple(
            () => Assert.True(union.HasFlag(FloodParam.Discharge)),
            () => Assert.True(union.HasFlag(FloodParam.DischargeMean)),
            () => Assert.Equal(2, Flags.Count(p => union.HasFlag(p))));
    }

    [Fact]
    public void Should_combine_all_flags_without_collision()
    {
        var all = Flags.Aggregate(FloodParam.None, (acc, p) => acc | p);

        Assert.Equal(Flags.Length, Flags.Count(p => all.HasFlag(p)));
    }
}
