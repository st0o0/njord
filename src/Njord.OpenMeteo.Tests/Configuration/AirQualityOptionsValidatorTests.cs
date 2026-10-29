using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.OpenMeteo.Configuration;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests.Configuration;

public sealed class AirQualityOptionsValidatorTests
{
    private readonly AirQualityOptionsValidator _validator = new();

    [Fact]
    public void Should_accept_the_default_options()
    {
        Assert.True(_validator.Validate(null, new AirQualityOptions()).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void Should_reject_forecast_days_outside_one_to_seven(int days)
    {
        var result = _validator.Validate(null, new AirQualityOptions { ForecastDays = days });

        Assert.Contains("AirQuality.ForecastDays must be between 1 and 7", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_an_undefined_region()
    {
        var result = _validator.Validate(null, new AirQualityOptions { Region = (AirQualityRegion)99 });

        Assert.Contains("AirQuality.Region", ValidationAssert.SingleFailure(result));
    }
}
