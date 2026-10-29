using Njord.Features.Alerts.WindChill;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.WindChill;

public sealed class WindChillConfigValidatorTests
{
    private readonly WindChillConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new WindChillConfig { Thresholds = WindChillConfig.DefaultThresholds }).Succeeded);
    }

    [Fact]
    public void Descending_thresholds_are_accepted()
    {
        Assert.True(_validator.Validate(null, new WindChillConfig { Thresholds = [-8, -18, -28] }).Succeeded);
    }

    [Fact]
    public void Ascending_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new WindChillConfig { Thresholds = [-30, -20, -10] });

        Assert.Contains("must be strictly descending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Fewer_than_three_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new WindChillConfig { Thresholds = [-10, -20] });

        Assert.Contains("needs exactly three values", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Equal_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new WindChillConfig { Thresholds = [-10, -10, -30] });

        Assert.Contains("must be strictly descending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_window_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new WindChillConfig { Thresholds = WindChillConfig.DefaultThresholds, WindowHours = 0 });

        Assert.Contains("WindowHours", ValidationAssert.SingleFailure(result));
    }
}
