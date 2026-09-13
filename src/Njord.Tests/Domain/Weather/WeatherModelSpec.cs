using Njord.Domain.Weather;

namespace Njord.Tests.Domain.Weather;

public sealed class WeatherModelSpec
{
    [Fact]
    public void Blank_model_ids_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new WeatherModel("  "));
    }

    [Fact]
    public void Ids_are_trimmed_and_compared_by_value()
    {
        Assert.Equal(new WeatherModel("icon_d2"), new WeatherModel(" icon_d2 "));
    }
}
