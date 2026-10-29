using Njord.Core.Configuration;
using Njord.OpenMeteo.Configuration;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests.Configuration;

public sealed class ForecastOptionsValidatorTests
{
    private readonly ForecastOptionsValidator _validator = new();

    [Fact]
    public void Should_accept_defaults_with_empty_models()
    {
        Assert.True(_validator.Validate(null, new ForecastOptions()).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void Should_reject_forecast_days_outside_one_to_sixteen(int days)
    {
        var result = _validator.Validate(null, new ForecastOptions { ForecastDays = days });

        Assert.Contains("Forecast.ForecastDays must be between 1 and 16", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(97)]
    public void Should_reject_past_hours_outside_zero_to_ninety_six(int hours)
    {
        var result = _validator.Validate(null, new ForecastOptions { PastHours = hours });

        Assert.Contains("Forecast.PastHours must be between 0 and 96", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_duplicate_model_naming_it()
    {
        var result = _validator.Validate(null, new ForecastOptions { Models = ["icon_seamless", "icon_seamless"] });

        Assert.Equal("Forecast.Models contains 'icon_seamless' more than once.", ValidationAssert.SingleFailure(result));
    }
}
