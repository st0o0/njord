using Njord.Features.Alerts.Visibility;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.Visibility;

public sealed class VisibilityConfigValidatorTests
{
    private readonly VisibilityConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new VisibilityConfig { Thresholds = VisibilityConfig.DefaultThresholds }).Succeeded);
    }

    [Fact]
    public void Descending_thresholds_are_accepted()
    {
        Assert.True(_validator.Validate(null, new VisibilityConfig { Thresholds = [800, 150, 30] }).Succeeded);
    }

    [Fact]
    public void Ascending_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new VisibilityConfig { Thresholds = [50, 200, 1000] });

        Assert.Contains("must be strictly descending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Fewer_than_three_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new VisibilityConfig { Thresholds = [1000, 200] });

        Assert.Contains("needs exactly three values", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Equal_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new VisibilityConfig { Thresholds = [1000, 1000, 50] });

        Assert.Contains("must be strictly descending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_window_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new VisibilityConfig { Thresholds = VisibilityConfig.DefaultThresholds, WindowHours = 0 });

        Assert.Contains("WindowHours", ValidationAssert.SingleFailure(result));
    }
}
