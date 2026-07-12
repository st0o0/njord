using Njord.Configuration;
using Njord.Domain;

namespace Njord.Tests.Configuration;

public sealed class ParameterOptionsValidationSpec
{
    private static readonly NjordOptionsValidator Validator = new();

    private static NjordOptions ValidOptions() => new()
    {
        Locations = [new LocationOptions { Name = "home", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2", "icon_eu", "icon_global", "ecmwf_ifs025", "gfs_seamless", "ukmo_global_deterministic_10km", "meteoswiss_icon_ch1", "meteoswiss_icon_ch2"],
        Mqtt = new MqttOptions { Host = "broker.local" },
    };

    [Fact(Timeout = 5000)]
    public void Unknown_group_is_rejected()
    {
        var options = ValidOptions();
        options.Parameters.Groups = ["NotAGroup"];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Unknown parameter group", result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void Unknown_extra_variable_is_rejected()
    {
        var options = ValidOptions();
        options.Parameters.Extra = ["completely_bogus_var"];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Unknown parameter in Extra", result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void Default_weather_group_passes_validation()
    {
        var options = ValidOptions();

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void Budget_projection_accounts_for_call_weight()
    {
        var options = ValidOptions();
        options.BudgetOverride = new RequestBudget(5_000, 600);

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("weight", result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void All_groups_enabled_still_passes_on_default_budget()
    {
        var options = ValidOptions();
        options.Parameters.Groups = ["Weather", "Solar", "Soil"];

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded, result.FailureMessage);
    }
}
