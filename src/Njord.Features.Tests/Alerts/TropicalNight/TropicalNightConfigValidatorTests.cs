using Njord.Features.Alerts.TropicalNight;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.TropicalNight;

public sealed class TropicalNightConfigValidatorTests
{
    private readonly TropicalNightConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new TropicalNightConfig { Thresholds = TropicalNightConfig.DefaultThresholds }).Succeeded);
    }

    [Fact]
    public void Ascending_thresholds_are_accepted()
    {
        Assert.True(_validator.Validate(null, new TropicalNightConfig { Thresholds = [18, 22, 26] }).Succeeded);
    }

    [Fact]
    public void Descending_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new TropicalNightConfig { Thresholds = [25, 23, 20] });

        Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Fewer_than_three_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new TropicalNightConfig { Thresholds = [20, 23] });

        Assert.Contains("needs exactly three values", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Equal_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new TropicalNightConfig { Thresholds = [20, 20, 25] });

        Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_window_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new TropicalNightConfig { Thresholds = TropicalNightConfig.DefaultThresholds, WindowHours = 0 });

        Assert.Contains("WindowHours", ValidationAssert.SingleFailure(result));
    }
}
