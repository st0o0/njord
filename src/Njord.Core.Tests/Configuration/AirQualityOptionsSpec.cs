using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class AirQualityOptionsSpec
{
    [Fact]
    public void Should_default_to_5_forecast_days_and_global_region()
    {
        var options = new AirQualityOptions();

        Assert.Multiple(
            () => Assert.Equal(5, options.ForecastDays),
            () => Assert.Equal(AirQualityRegion.Global, options.Region),
            () => Assert.True(new AirQualityOptionsValidator().Validate(null, options).Succeeded));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void Should_accept_forecast_days_within_the_limits(int days)
    {
        var result = new AirQualityOptionsValidator().Validate(null, new AirQualityOptions { ForecastDays = days });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void Should_reject_forecast_days_outside_one_to_7(int days)
    {
        var result = new AirQualityOptionsValidator().Validate(null, new AirQualityOptions { ForecastDays = days });

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData(AirQualityRegion.Global)]
    [InlineData(AirQualityRegion.Europe)]
    [InlineData(AirQualityRegion.UnitedStates)]
    public void Should_accept_valid_regions(AirQualityRegion region)
    {
        var result = new AirQualityOptionsValidator().Validate(null, new AirQualityOptions { Region = region });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Should_reject_undefined_region()
    {
        var result = new AirQualityOptionsValidator().Validate(null, new AirQualityOptions { Region = (AirQualityRegion)99 });

        Assert.True(result.Failed);
    }
}
