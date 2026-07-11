using Njord.Domain;

namespace Njord.Tests.Domain;

public sealed class WeatherParameterSpec
{
    [Fact(Timeout = 5000)]
    public void Wind_speed_is_measured_in_meters_per_second()
    {
        Assert.Equal("m/s", WeatherParameter.WindSpeed.Unit());
    }

    [Fact(Timeout = 5000)]
    public void Every_parameter_has_a_unit()
    {
        foreach (var parameter in Enum.GetValues<WeatherParameter>())
        {
            Assert.False(string.IsNullOrWhiteSpace(parameter.Unit()));
        }
    }

    [Fact(Timeout = 5000)]
    public void Apparent_temperature_is_measured_in_celsius()
    {
        Assert.Equal("°C", WeatherParameter.ApparentTemperature.Unit());
    }

    [Fact(Timeout = 5000)]
    public void The_v1_parameter_set_is_exactly_nine_wide()
    {
        Assert.Equal(9, Enum.GetValues<WeatherParameter>().Length);
    }
}
