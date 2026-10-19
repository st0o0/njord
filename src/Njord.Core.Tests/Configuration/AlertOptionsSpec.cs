using Njord.Core.Configuration;
using Njord.Messages.Alerts;

namespace Njord.Core.Tests.Configuration;

public sealed class AlertOptionsSpec
{
    private readonly AlertOptionsValidator _validator = new();

    [Fact]
    public void Should_build_minimum_thresholds_for_frost_from_the_configured_values()
    {
        var thresholds = new AlertOptions { FrostThresholds = [1, -2, -8] }.Frost();

        Assert.Equal(new SeverityThresholds(1, -2, -8, ScanDirection.Min), thresholds);
    }

    [Fact]
    public void Should_build_maximum_thresholds_for_heat_from_the_configured_values()
    {
        var thresholds = new AlertOptions().Heat();

        Assert.Equal(new SeverityThresholds(30, 35, 40, ScanDirection.Max), thresholds);
    }

    [Fact]
    public void Should_throw_when_a_threshold_array_does_not_have_three_values()
    {
        var options = new AlertOptions { StormGustThresholds = [17, 25] };

        var ex = Assert.Throws<InvalidOperationException>(options.StormGust);

        Assert.Contains("StormGustThresholds", ex.Message);
    }

    [Fact]
    public void Should_reject_rising_frost_thresholds()
    {
        var result = _validator.Validate(null, new AlertOptions { FrostThresholds = [-15, -5, 0] });

        Assert.True(result.Failed);
        Assert.Contains("FrostThresholds must fall", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_falling_heat_thresholds()
    {
        var result = _validator.Validate(null, new AlertOptions { HeatThresholds = [40, 35, 30] });

        Assert.True(result.Failed);
        Assert.Contains("HeatThresholds must rise", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_threshold_arrays_with_the_wrong_length()
    {
        var result = _validator.Validate(null, new AlertOptions { HumidityThresholds = [16, 21] });

        Assert.True(result.Failed);
        Assert.Contains("needs exactly three values", ValidationAssert.SingleFailure(result));
    }
}
