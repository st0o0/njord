using Njord.OpenMeteo.Configuration;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests.Configuration;

public sealed class MarineOptionsValidatorTests
{
    private readonly MarineOptionsValidator _validator = new();

    [Fact]
    public void Should_accept_the_default_options()
    {
        Assert.True(_validator.Validate(null, new MarineOptions()).Succeeded);
    }

    [Fact]
    public void Should_accept_sixteen_forecast_days()
    {
        Assert.True(_validator.Validate(null, new MarineOptions { ForecastDays = 16 }).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void Should_reject_forecast_days_outside_one_to_sixteen(int value)
    {
        var result = _validator.Validate(null, new MarineOptions { ForecastDays = value });

        Assert.Contains("Marine.ForecastDays must be between 1 and 16", ValidationAssert.SingleFailure(result));
    }
}
