using Njord.Core.Domain;
using Njord.Forecast.Weather;

namespace Njord.Core.Tests.Domain;

public sealed class WeatherParamExtensionsSpec
{
    [Fact]
    public void Should_count_no_flags_as_zero()
    {
        Assert.Equal(0, WeatherParam.None.FlagCount());
    }

    [Fact]
    public void Should_count_each_set_flag_once()
    {
        Assert.Equal(3, (WeatherParam.Temperature | WeatherParam.UvIndex | WeatherParam.WindDirection).FlagCount());
    }

    [Fact]
    public void Should_count_every_defined_flag()
    {
        var all = Enum.GetValues<WeatherParam>().Aggregate(WeatherParam.None, (a, b) => a | b);

        Assert.Equal(Enum.GetValues<WeatherParam>().Length - 1, all.FlagCount());
    }
}
