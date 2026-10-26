using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.OpenMeteo.Configuration;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoOptionsValidatorTests
{
    private readonly OpenMeteoOptionsValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void Should_reject_weather_forecast_days_outside_one_to_sixteen(int days)
    {
        var result = _validator.Validate(null, new WeatherOptions { ForecastDays = days });

        Assert.Contains("Weather.ForecastDays must be between 1 and 16", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_an_empty_model_list()
    {
        var result = _validator.Validate(null, new WeatherOptions { Models = [] });

        Assert.Equal("Weather.Models must contain at least one model.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_duplicate_model_naming_it()
    {
        var result = _validator.Validate(null, new WeatherOptions { Models = ["ecmwf_ifs025_ensemble_mean", "ecmwf_ifs025_ensemble_mean"] });

        Assert.Equal("Weather.Models contains 'ecmwf_ifs025_ensemble_mean' more than once.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_an_unknown_model_naming_it()
    {
        var result = _validator.Validate(null, new WeatherOptions { Models = ["ecmwf_ifs025_ensemble_mean", "gfs_ensemble"] });

        Assert.Contains("Weather.Models entry 'gfs_ensemble' is not a known model", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_the_legacy_model_key_pointing_to_models()
    {
        var result = _validator.Validate(null, new WeatherOptions { Model = "ecmwf_ifs025_ensemble_mean" });

        Assert.Contains("Njord:Weather:Models", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_accept_all_catalog_models()
    {
        var result = _validator.Validate(null, new WeatherOptions { Models = [.. WeatherModelCatalog.All.Select(m => m.Id)] });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void Should_reject_air_quality_forecast_days_outside_one_to_seven(int days)
    {
        var result = _validator.Validate(null, new AirQualityOptions { ForecastDays = days });

        Assert.Contains("AirQuality.ForecastDays must be between 1 and 7", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_an_undefined_air_quality_region()
    {
        var result = _validator.Validate(null, new AirQualityOptions { Region = (AirQualityRegion)99 });

        Assert.Contains("AirQuality.Region", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(211)]
    public void Should_reject_flood_forecast_days_outside_one_to_210(int days)
    {
        var result = _validator.Validate(null, new FloodOptions { ForecastDays = days });

        Assert.Contains("Flood.ForecastDays must be between 1 and 210", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Should_reject_marine_forecast_days_outside_one_to_eight(int days)
    {
        var result = _validator.Validate(null, new MarineOptions { ForecastDays = days });

        Assert.Contains("Marine.ForecastDays must be between 1 and 8", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Should_reject_seasonal_forecast_months_outside_one_to_six(int months)
    {
        var result = _validator.Validate(null, new SeasonalOptions { ForecastMonths = months });

        Assert.Contains("Seasonal.ForecastMonths must be between 1 and 6", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(49)]
    public void Should_reject_satellite_past_hours_outside_one_to_48(int hours)
    {
        var result = _validator.Validate(null, new SatelliteOptions { PastHours = hours });

        Assert.Contains("Satellite.PastHours must be between 1 and 48", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_non_positive_solar_peak()
    {
        var result = _validator.Validate(null, new SolarYieldOptions { PeakKw = 0 });

        Assert.Contains("SolarYield.PeakKw must be positive", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(361)]
    public void Should_reject_a_solar_azimuth_outside_zero_to_360(int azimuth)
    {
        var result = _validator.Validate(null, new SolarYieldOptions { Azimuth = azimuth });

        Assert.Contains("SolarYield.Azimuth must be between 0 and 360", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(91)]
    public void Should_reject_a_solar_tilt_outside_zero_to_90(int tilt)
    {
        var result = _validator.Validate(null, new SolarYieldOptions { Tilt = tilt });

        Assert.Contains("SolarYield.Tilt must be between 0 and 90", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_accept_the_default_options()
    {
        Assert.Multiple(
            () => Assert.True(_validator.Validate(null, new WeatherOptions()).Succeeded),
            () => Assert.True(_validator.Validate(null, new AirQualityOptions()).Succeeded),
            () => Assert.True(_validator.Validate(null, new FloodOptions()).Succeeded),
            () => Assert.True(_validator.Validate(null, new MarineOptions()).Succeeded),
            () => Assert.True(_validator.Validate(null, new SeasonalOptions()).Succeeded),
            () => Assert.True(_validator.Validate(null, new SatelliteOptions()).Succeeded),
            () => Assert.True(_validator.Validate(null, new SolarYieldOptions()).Succeeded));
    }
}
