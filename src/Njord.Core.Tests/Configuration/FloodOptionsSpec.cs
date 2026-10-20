using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class FloodOptionsSpec
{
    [Fact]
    public void Should_default_to_92_forecast_days()
    {
        var options = new FloodOptions();

        Assert.Multiple(
            () => Assert.Equal(92, options.ForecastDays),
            () => Assert.False(options.IncludeEnsemble),
            () => Assert.True(new FloodOptionsValidator().Validate(null, options).Succeeded));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(210)]
    public void Should_accept_forecast_days_within_the_limits(int days)
    {
        var result = new FloodOptionsValidator().Validate(null, new FloodOptions { ForecastDays = days });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(211)]
    public void Should_reject_forecast_days_outside_one_to_210(int days)
    {
        var result = new FloodOptionsValidator().Validate(null, new FloodOptions { ForecastDays = days });

        Assert.Equal("ForecastDays must be between 1 and 210.", ValidationAssert.SingleFailure(result));
    }
}
