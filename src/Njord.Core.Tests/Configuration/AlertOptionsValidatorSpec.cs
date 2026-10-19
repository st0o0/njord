using Njord.Core.Configuration;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration;

public sealed class AlertOptionsValidatorSpec
{
    private static readonly AlertOptionsValidator Validator = new();

    [Fact]
    public void Default_thresholds_are_valid()
    {
        var result = Validator.Validate(null, AppSettingsDefaults.Options<AlertOptions>());

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void Frost_thresholds_that_do_not_fall_are_rejected()
    {
        var result = Validator.Validate(null, AppSettingsDefaults.Options<AlertOptions>(o => o.FrostThresholds = [0, 5, -15]));

        Assert.True(result.Failed);
        Assert.Contains("FrostThresholds", result.FailureMessage);
    }

    [Fact]
    public void Heat_thresholds_that_do_not_rise_are_rejected()
    {
        var result = Validator.Validate(null, AppSettingsDefaults.Options<AlertOptions>(o => o.HeatThresholds = [35, 30, 40]));

        Assert.True(result.Failed);
        Assert.Contains("HeatThresholds", result.FailureMessage);
    }

    [Fact]
    public void Threshold_arrays_need_three_values()
    {
        var result = Validator.Validate(null, AppSettingsDefaults.Options<AlertOptions>(o => o.StormGustThresholds = [17, 25]));

        Assert.True(result.Failed);
        Assert.Contains("StormGustThresholds", result.FailureMessage);
        Assert.Contains("three", result.FailureMessage);
    }

    [Fact]
    public void Equal_neighbouring_levels_are_accepted()
    {
        var result = Validator.Validate(null, AppSettingsDefaults.Options<AlertOptions>(o => o.VisibilityThresholds = [1000, 1000, 50]));

        Assert.True(result.Succeeded, result.FailureMessage);
    }
}
