using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class MarineOptionsSpec
{
    [Fact]
    public void Should_default_to_8_forecast_days()
    {
        var options = new MarineOptions();

        Assert.Multiple(
            () => Assert.Equal(8, options.ForecastDays),
            () => Assert.True(new MarineOptionsValidator().Validate(null, options).Succeeded));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void Should_accept_forecast_days_within_the_limits(int days)
    {
        var result = new MarineOptionsValidator().Validate(null, new MarineOptions { ForecastDays = days });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Should_reject_forecast_days_outside_one_to_8(int days)
    {
        var result = new MarineOptionsValidator().Validate(null, new MarineOptions { ForecastDays = days });

        Assert.Equal("ForecastDays must be between 1 and 8.", ValidationAssert.SingleFailure(result));
    }
}
