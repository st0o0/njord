using Njord.Core.Forecast;

namespace Njord.Core.Tests.Forecast;

public sealed class ForecastParamsTests
{
    private static readonly ForecastParams[] _flags =
        [.. Enum.GetValues<ForecastParams>().Where(p => p != ForecastParams.None)];

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
        var union = ForecastParams.Temperature | ForecastParams.ApparentTemperature;

        Assert.Multiple(
            () => Assert.True(union.HasFlag(ForecastParams.Temperature)),
            () => Assert.True(union.HasFlag(ForecastParams.ApparentTemperature)),
            () => Assert.Equal(2, _flags.Count(p => union.HasFlag(p))));
    }

    [Fact]
    public void Should_combine_all_flags_without_collision()
    {
        var all = _flags.Aggregate(ForecastParams.None, (acc, p) => acc | p);

        Assert.Equal(_flags.Length, _flags.Count(p => all.HasFlag(p)));
    }

    [Fact]
    public void Should_keep_flags_above_32_bits_distinct_when_combined()
    {
        var high = (ForecastParams)(1L << 40);
        var union = ForecastParams.Temperature | high;

        Assert.Multiple(
            () => Assert.Equal(typeof(long), Enum.GetUnderlyingType(typeof(ForecastParams))),
            () => Assert.True(union.HasFlag(high)),
            () => Assert.True(union.HasFlag(ForecastParams.Temperature)),
            () => Assert.False((ForecastParams.Temperature | ForecastParams.WindSpeed).HasFlag(high)));
    }
}
