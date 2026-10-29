using Njord.Features.Alerts.Frost;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.Frost;

public sealed class FrostConfigValidatorTests
{
    private readonly FrostConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new FrostConfig { Thresholds = FrostConfig.DefaultThresholds }).Succeeded);
    }

    [Fact]
    public void Descending_thresholds_are_accepted()
    {
        Assert.True(_validator.Validate(null, new FrostConfig { Thresholds = [1, -4, -10] }).Succeeded);
    }

    [Fact]
    public void Ascending_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new FrostConfig { Thresholds = [-15, -5, 0] });

        Assert.Contains("must be strictly descending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Fewer_than_three_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new FrostConfig { Thresholds = [0, -5] });

        Assert.Contains("needs exactly three values", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Equal_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new FrostConfig { Thresholds = [0, 0, -15] });

        Assert.Contains("must be strictly descending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_window_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new FrostConfig { Thresholds = FrostConfig.DefaultThresholds, WindowHours = 0 });

        Assert.Contains("WindowHours", ValidationAssert.SingleFailure(result));
    }
}
