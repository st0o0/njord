using Njord.Configuration;

namespace Njord.Tests.Configuration;

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

    [Fact(Timeout = 5000)]
    public void Default_configuration_passes()
    {
        var result = Validator.Validate(null, ValidOptions());

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void A_projection_above_the_override_budget_guard_is_rejected()
    {
        var options = ValidOptions();
        options.BudgetOverride = new RequestBudget(10_000, 600);
        options.Locations = [.. Enumerable.Range(1, 2).Select(i => new LocationOptions
        {
            Name = $"loc-{i}",
            Latitude = 47.0,
            Longitude = 8.0,
        })];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("11520", result.FailureMessage);
        Assert.Contains("8000", result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void Empty_model_list_is_rejected()
    {
        var options = ValidOptions();
        options.Models = [];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("model", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Timeout = 5000)]
    public void Blank_model_entries_are_rejected()
    {
        var options = ValidOptions();
        options.Models = ["icon_d2", "  "];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact(Timeout = 5000)]
    public void Poll_interval_defaults_to_sixty_minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(60), new NjordOptions().PollInterval);
    }
}
