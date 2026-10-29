using Njord.Core.Configuration;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration;

public sealed class FloodOptionsValidatorTests
{
    private readonly FloodOptionsValidator _validator = new();

    [Fact]
    public void Should_accept_the_default_options()
    {
        Assert.True(_validator.Validate(null, new FloodOptions()).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(211)]
    public void Should_reject_forecast_days_outside_one_to_210(int value)
    {
        var result = _validator.Validate(null, new FloodOptions { ForecastDays = value });

        Assert.Contains("Flood.ForecastDays must be between 1 and 210", ValidationAssert.SingleFailure(result));
    }
}
