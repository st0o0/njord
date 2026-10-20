using Njord.Core.Domain;
using Njord.Forecast.Weather;

namespace Njord.Core.Tests.Domain;

public sealed class WeatherParamExtensionsSpec
{
    [Fact]
    public void Should_count_no_flags_as_zero()
    {
        Assert.Equal(0, WeatherParams.None.FlagCount());
    }

    [Fact]
    public void Should_count_each_set_flag_once()
    {
        Assert.Equal(3, (WeatherParams.Temperature | WeatherParams.UvIndex | WeatherParams.WindDirection).FlagCount());
    }

    [Fact]
    public void Should_count_every_defined_flag()
    {
        var all = Enum.GetValues<WeatherParams>().Aggregate(WeatherParams.None, (a, b) => a | b);

        Assert.Equal(Enum.GetValues<WeatherParams>().Length - 1, all.FlagCount());
    }
}
