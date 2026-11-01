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

    [Fact]
    public void Should_accept_seven_forecast_months()
    {
        Assert.True(_validator.Validate(null, new SeasonalOptions { ForecastMonths = 7 }).Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Should_reject_an_empty_model(string model)
    {
        var result = _validator.Validate(null, new SeasonalOptions { Model = model });

        Assert.Contains("Seasonal.Model must not be empty", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void Should_reject_forecast_months_outside_one_to_seven(int value)
    {
        var result = _validator.Validate(null, new SeasonalOptions { ForecastMonths = value });

        Assert.Contains("Seasonal.ForecastMonths must be between 1 and 7", ValidationAssert.SingleFailure(result));
    }
}
