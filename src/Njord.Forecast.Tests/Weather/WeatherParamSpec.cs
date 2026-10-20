using Njord.Forecast.Weather;

namespace Njord.Forecast.Tests.Weather;

public sealed class WeatherParamSpec
{
    private static readonly WeatherParam[] Flags =
        [.. Enum.GetValues<WeatherParam>().Where(p => p != WeatherParam.None)];

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
        var union = WeatherParam.Temperature | WeatherParam.ApparentTemperature;

        Assert.Multiple(
            () => Assert.True(union.HasFlag(WeatherParam.Temperature)),
            () => Assert.True(union.HasFlag(WeatherParam.ApparentTemperature)),
            () => Assert.Equal(2, Flags.Count(p => union.HasFlag(p))));
    }

    [Fact]
    public void Should_combine_all_flags_without_collision()
    {
        var all = Flags.Aggregate(WeatherParam.None, (acc, p) => acc | p);

        Assert.Equal(Flags.Length, Flags.Count(p => all.HasFlag(p)));
    }

    [Fact]
    public void Should_keep_flags_above_32_bits_distinct_when_combined()
    {
        var high = (WeatherParam)(1L << 40);
        var union = WeatherParam.Temperature | high;

        Assert.Multiple(
            () => Assert.Equal(typeof(long), Enum.GetUnderlyingType(typeof(WeatherParam))),
            () => Assert.True(union.HasFlag(high)),
            () => Assert.True(union.HasFlag(WeatherParam.Temperature)),
            () => Assert.False((WeatherParam.Temperature | WeatherParam.WindSpeed).HasFlag(high)));
    }
}
