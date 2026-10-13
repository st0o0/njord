using Njord.Core.Configuration;
using Njord.Weather;

namespace Njord.Core.Tests.Configuration;

public sealed class NjordOptionsValidatorSpec
{
    private static NjordOptions ValidOptions() => new()
    {
        Locations = [new LocationOptions { Name = "home", Latitude = 47.05, Longitude = 8.31 }],
        Models =
        [
            "icon_d2", "icon_eu", "icon_global", "ecmwf_ifs025",
            "gfs_seamless", "ukmo_global_deterministic_10km",
            "meteoswiss_icon_ch1", "meteoswiss_icon_ch2",
        ],
    };

    private static readonly NjordOptionsValidator Validator = new();

    [Fact]
    public void Default_configuration_passes()
    {
        var result = Validator.Validate(null, ValidOptions());

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void Empty_location_list_is_rejected()
    {
        var options = ValidOptions();
        options.Locations = [];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("location", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Empty_model_list_is_rejected()
    {
        var options = ValidOptions();
        options.Models = [];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("model", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Blank_model_entries_are_rejected()
    {
        var options = ValidOptions();
        options.Models = ["icon_d2", "  "];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Empty_horizons_are_rejected()
    {
        var options = ValidOptions();
        options.Horizons = [];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("horizon", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Non_positive_horizons_are_rejected()
    {
        var options = ValidOptions();
        options.Horizons = [3, 0];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Horizons_beyond_the_fetch_window_are_rejected()
    {
        var options = ValidOptions();
        options.Horizons = [3, 120];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("96", result.FailureMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void Forecast_days_outside_one_to_sixteen_are_rejected(int days)
    {
        var options = ValidOptions();
        options.ForecastDays = days;

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ForecastDays", result.FailureMessage);
    }

    [Fact]
    public void A_model_outside_its_documented_coverage_is_rejected()
    {
        var options = ValidOptions();
        options.Locations = [new LocationOptions { Name = "sydney", Latitude = -33.87, Longitude = 151.21 }];
        options.Models = ["icon_d2"];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("icon_d2", result.FailureMessage);
    }
}
