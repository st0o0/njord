using Microsoft.Extensions.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration.Polling;

public sealed class PollingOptionsValidatorSpec
{
    private static PollingOptionsValidator ValidatorFor(int locations, params string[] models)
    {
        var entries = new Dictionary<string, string?>();
        for (var i = 0; i < locations; i++)
        {
            entries[$"Njord:Locations:{i}:Name"] = $"loc-{i}";
            entries[$"Njord:Locations:{i}:Latitude"] = "47.0";
            entries[$"Njord:Locations:{i}:Longitude"] = "8.0";
        }

        for (var i = 0; i < models.Length; i++)
        {
            entries[$"Njord:Models:{i}"] = models[i];
        }

        return new PollingOptionsValidator(AppSettingsDefaults.Builder().AddInMemoryCollection(entries).Build());
    }

    private static readonly string[] EightModels =
    [
        "icon_d2", "icon_eu", "icon_global", "ecmwf_ifs025",
        "gfs_seamless", "ukmo_global_deterministic_10km",
        "meteoswiss_icon_ch1", "meteoswiss_icon_ch2",
    ];

    [Fact]
    public void Defaults_pass_for_one_location_with_eight_models()
    {
        var result = ValidatorFor(1, EightModels).Validate(null, AppSettingsDefaults.Options<PollingOptions>());

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void A_projection_above_the_override_budget_guard_is_rejected()
    {
        var polling = AppSettingsDefaults.Options<PollingOptions>(o => o.BudgetOverride = new RequestBudget(10_000, 600));

        var result = ValidatorFor(2, EightModels).Validate(null, polling);

        Assert.True(result.Failed);
        Assert.Contains("8000", result.FailureMessage);
        Assert.Contains("weight", result.FailureMessage);
    }

    [Fact]
    public void A_poll_interval_whose_projection_exceeds_the_guard_is_rejected()
    {
        var polling = AppSettingsDefaults.Options<PollingOptions>(o => o.PollInterval = TimeSpan.FromMinutes(1));

        var result = ValidatorFor(1, EightModels).Validate(null, polling);

        Assert.True(result.Failed);
        Assert.Contains("80%", result.FailureMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_intervals_are_rejected(int minutes)
    {
        var polling = AppSettingsDefaults.Options<PollingOptions>(o =>
        {
            o.PollInterval = TimeSpan.FromMinutes(minutes);
            o.DiscoveryInterval = TimeSpan.FromMinutes(minutes);
        });

        var result = ValidatorFor(1, "icon_d2").Validate(null, polling);

        Assert.True(result.Failed);
        Assert.Contains("PollInterval", result.FailureMessage);
        Assert.Contains("DiscoveryInterval", result.FailureMessage);
    }

    [Fact]
    public void A_non_positive_budget_override_is_rejected()
    {
        var polling = AppSettingsDefaults.Options<PollingOptions>(o => o.BudgetOverride = new RequestBudget(0, 600));

        var result = ValidatorFor(1, "icon_d2").Validate(null, polling);

        Assert.True(result.Failed);
        Assert.Contains("BudgetOverride", result.FailureMessage);
    }

    [Fact]
    public void Without_a_forecast_scope_the_budget_projection_is_skipped()
    {
        var polling = AppSettingsDefaults.Options<PollingOptions>(o => o.BudgetOverride = new RequestBudget(1, 1));

        var result = ValidatorFor(0).Validate(null, polling);

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void All_parameter_groups_still_pass_on_the_default_budget()
    {
        var configuration = AppSettingsDefaults.Builder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Njord:Locations:0:Name"] = "home",
            ["Njord:Locations:0:Latitude"] = "47.05",
            ["Njord:Locations:0:Longitude"] = "8.31",
            ["Njord:Models:0"] = "icon_d2",
            ["Njord:Parameters:Groups:0"] = "Weather",
            ["Njord:Parameters:Groups:1"] = "Solar",
            ["Njord:Parameters:Groups:2"] = "Soil",
        }).Build();

        var result = new PollingOptionsValidator(configuration).Validate(null, AppSettingsDefaults.Options<PollingOptions>());

        Assert.True(result.Succeeded, result.FailureMessage);
    }
}
