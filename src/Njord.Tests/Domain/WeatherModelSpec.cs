using Njord.Domain;

namespace Njord.Tests.Domain;

public sealed class WeatherModelSpec
{
    [Fact(Timeout = 5000)]
    public void Blank_model_ids_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new WeatherModel("  "));
    }

    [Fact(Timeout = 5000)]
    public void Ids_are_trimmed_and_compared_by_value()
    {
        Assert.Equal(new WeatherModel("icon_d2"), new WeatherModel(" icon_d2 "));
    }
}
