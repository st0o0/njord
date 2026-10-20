using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class WeatherOptionsSpec
{
    [Fact]
    public void Should_default_to_no_solar_and_four_forecast_days()
    {
        var options = new WeatherOptions();

        Assert.Multiple(
            () => Assert.False(options.IncludeSolar),
            () => Assert.Equal(4, options.ForecastDays),
            () => Assert.True(new WeatherOptionsValidator().Validate(null, options).Succeeded));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void Should_reject_forecast_days_outside_one_to_sixteen(int days)
    {
        var result = new WeatherOptionsValidator().Validate(null, new WeatherOptions { ForecastDays = days });

        Assert.Equal("ForecastDays must be between 1 and 16.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_add_the_solar_variables_only_when_requested()
    {
        var plain = WeatherVariables.For(false);
        var solar = WeatherVariables.For(true);

        Assert.Multiple(
            () => Assert.DoesNotContain("shortwave_radiation", plain),
            () => Assert.Equal(["shortwave_radiation", "direct_radiation", "diffuse_radiation", "direct_normal_irradiance", "sunshine_duration"], solar.Skip(plain.Count)),
            () => Assert.Equal(plain.Count + 5, solar.Count));
    }

    [Theory]
    [InlineData(10, 1)]
    [InlineData(11, 2)]
    [InlineData(31, 4)]
    public void Should_weigh_one_call_per_ten_variables(int count, int weight)
    {
        Assert.Equal(weight, WeatherVariables.ApiCallWeight(count));
    }
}
