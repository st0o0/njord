using Njord.Core.Configuration;
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

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Should_reject_forecast_days_outside_one_to_eight(int value)
    {
        var result = _validator.Validate(null, new MarineOptions { ForecastDays = value });

        Assert.Contains("Marine.ForecastDays must be between 1 and 8", ValidationAssert.SingleFailure(result));
    }
}
