using Microsoft.Extensions.Configuration;
using Njord.Core.Configuration;
using Njord.Tests.Shared;
using Njord.Model;

namespace Njord.Core.Tests.Configuration;

public sealed class NjordOptionsValidatorSpec
{
    private static NjordOptions ValidOptions() => AppSettingsDefaults.Options<NjordOptions>(o =>
    {
        o.Locations = [new LocationOptions { Name = "home", Latitude = 47.05, Longitude = 8.31 }];
        o.Models =
        [
            "icon_d2", "icon_eu", "icon_global", "ecmwf_ifs025",
            "gfs_seamless", "ukmo_global_deterministic_10km",
            "meteoswiss_icon_ch1", "meteoswiss_icon_ch2",
        ];
    });

    private static readonly NjordOptionsValidator Validator = new(new ConfigurationBuilder().Build());

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

    [Theory]
    [InlineData("Njord:PollInterval", "00:30:00", "Njord:Polling:PollInterval")]
    [InlineData("Njord:DiscoveryInterval", "00:10:00", "Njord:Polling:DiscoveryInterval")]
    [InlineData("Njord:BudgetOverride:RequestsPerMonth", "100000", "Njord:Polling:BudgetOverride")]
    [InlineData("Njord:OpenMeteoBaseUrl", "http://localhost:8080", "Njord:OpenMeteo:BaseUrl")]
    [InlineData("Njord:PersistencePath", "data/other.db", "Njord:Storage:PersistencePath")]
    public void A_removed_root_level_key_fails_naming_the_old_and_the_new_key(string key, string value, string newKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();
        var validator = new NjordOptionsValidator(configuration);

        var result = validator.Validate(null, ValidOptions());

        var oldKey = string.Join(':', key.Split(':').Take(2));
        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures!);
        Assert.Equal(
            $"Configuration key '{oldKey}' was removed in 0.5.0; use '{newKey}' "
            + $"(environment variable '{newKey.Replace(":", "__")}') instead.",
            failure);
    }

    [Fact]
    public void A_removed_key_from_the_environment_is_reported()
    {
        var name = $"NJORD_SPEC_{Guid.NewGuid():N}__";
        Environment.SetEnvironmentVariable($"{name}Njord__PollInterval", "00:30:00");
        try
        {
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables(name).Build();

            var result = new NjordOptionsValidator(configuration).Validate(null, ValidOptions());

            Assert.Contains("'Njord:PollInterval'", Assert.Single(result.Failures!));
        }
        finally
        {
            Environment.SetEnvironmentVariable($"{name}Njord__PollInterval", null);
        }
    }

    [Fact]
    public void The_new_keys_do_not_trigger_the_legacy_check()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Njord:Polling:PollInterval"] = "00:30:00",
                ["Njord:OpenMeteo:BaseUrl"] = "http://localhost:8080",
                ["Njord:Storage:PersistencePath"] = "data/other.db",
            })
            .Build();

        var result = new NjordOptionsValidator(configuration).Validate(null, ValidOptions());

        Assert.True(result.Succeeded, result.FailureMessage);
    }
}
