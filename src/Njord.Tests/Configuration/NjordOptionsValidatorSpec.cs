using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class NjordOptionsValidatorSpec
{
    private static NjordOptions ValidOptions() => new()
    {
        ApiKey = "test-key",
        Plan = NjordPlan.Hobby,
        Locations = [new LocationOptions { Name = "home", Latitude = 47.0, Longitude = 8.0 }],
        Models = ["ICON-D2", "ECMWF", "GFS", "SWISS1X1"],
    };

    private static readonly NjordOptionsValidator Validator = new();

    [Fact(Timeout = 5000)]
    public void Default_configuration_passes()
    {
        var result = Validator.Validate(null, ValidOptions());

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void Six_locations_on_hobby_defaults_exceed_the_budget_guard()
    {
        var options = ValidOptions();
        options.Locations = [.. Enumerable.Range(1, 6).Select(i => new LocationOptions
        {
            Name = $"loc-{i}",
            Latitude = 47.0,
            Longitude = 8.0,
        })];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("17280", result.FailureMessage);
        Assert.Contains("16000", result.FailureMessage);
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
    public void Missing_api_key_is_rejected()
    {
        var options = ValidOptions();
        options.ApiKey = "";

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Njord__ApiKey", result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void Custom_plan_without_override_is_rejected()
    {
        var options = ValidOptions();
        options.Plan = NjordPlan.Custom;
        options.BudgetOverride = null;

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("BudgetOverride", result.FailureMessage);
    }

    [Fact(Timeout = 5000)]
    public void Blank_model_entries_are_rejected()
    {
        var options = ValidOptions();
        options.Models = ["ICON-D2", "  "];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact(Timeout = 5000)]
    public void Poll_interval_defaults_to_sixty_minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(60), new NjordOptions().PollInterval);
    }

    [Fact(Timeout = 5000)]
    public void Failure_messages_never_contain_the_api_key()
    {
        var options = ValidOptions();
        options.ApiKey = "super-secret-key";
        options.Models = [];

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.DoesNotContain("super-secret-key", result.FailureMessage);
    }
}
