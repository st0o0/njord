using Njord.Features.Alerts.Heat;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.Heat;

public sealed class HeatConfigValidatorTests
{
    private readonly HeatConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new HeatConfig { Thresholds = HeatConfig.DefaultThresholds }).Succeeded);
    }

    [Fact]
    public void Ascending_thresholds_are_accepted()
    {
        Assert.True(_validator.Validate(null, new HeatConfig { Thresholds = [28, 33, 38] }).Succeeded);
    }

    [Fact]
    public void Descending_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new HeatConfig { Thresholds = [40, 35, 30] });

        Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Fewer_than_three_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new HeatConfig { Thresholds = [30, 35] });

        Assert.Contains("needs exactly three values", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Equal_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new HeatConfig { Thresholds = [30, 30, 40] });

        Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_window_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new HeatConfig { Thresholds = HeatConfig.DefaultThresholds, WindowHours = 0 });

        Assert.Contains("WindowHours", ValidationAssert.SingleFailure(result));
    }
}
