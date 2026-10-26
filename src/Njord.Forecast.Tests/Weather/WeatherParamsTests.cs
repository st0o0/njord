using Njord.Forecast.Weather;

namespace Njord.Forecast.Tests.Weather;

public sealed class WeatherParamsTests
{
    private static readonly WeatherParams[] Flags =
        [.. Enum.GetValues<WeatherParams>().Where(p => p != WeatherParams.None)];

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
        var union = WeatherParams.Temperature | WeatherParams.ApparentTemperature;

        Assert.Multiple(
            () => Assert.True(union.HasFlag(WeatherParams.Temperature)),
            () => Assert.True(union.HasFlag(WeatherParams.ApparentTemperature)),
            () => Assert.Equal(2, Flags.Count(p => union.HasFlag(p))));
    }

    [Fact]
    public void Should_combine_all_flags_without_collision()
    {
        var all = Flags.Aggregate(WeatherParams.None, (acc, p) => acc | p);

        Assert.Equal(Flags.Length, Flags.Count(p => all.HasFlag(p)));
    }

    [Fact]
    public void Should_keep_flags_above_32_bits_distinct_when_combined()
    {
        var high = (WeatherParams)(1L << 40);
        var union = WeatherParams.Temperature | high;

        Assert.Multiple(
            () => Assert.Equal(typeof(long), Enum.GetUnderlyingType(typeof(WeatherParams))),
            () => Assert.True(union.HasFlag(high)),
            () => Assert.True(union.HasFlag(WeatherParams.Temperature)),
            () => Assert.False((WeatherParams.Temperature | WeatherParams.WindSpeed).HasFlag(high)));
    }
}
