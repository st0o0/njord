using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class WeatherOptionsSpec
{
    [Fact]
    public void Should_default_to_four_forecast_days()
    {
        var options = new WeatherOptions();

        Assert.Multiple(
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
}
