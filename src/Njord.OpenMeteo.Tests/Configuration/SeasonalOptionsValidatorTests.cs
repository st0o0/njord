using Njord.Core.Configuration;
using Njord.OpenMeteo.Configuration;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests.Configuration;

public sealed class SeasonalOptionsValidatorTests
{
    private readonly SeasonalOptionsValidator _validator = new();

    [Fact]
    public void Should_accept_the_default_options()
    {
        Assert.True(_validator.Validate(null, new SeasonalOptions()).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Should_reject_forecast_months_outside_one_to_six(int value)
    {
        var result = _validator.Validate(null, new SeasonalOptions { ForecastMonths = value });

        Assert.Contains("Seasonal.ForecastMonths must be between 1 and 6", ValidationAssert.SingleFailure(result));
    }
}
