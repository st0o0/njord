using Njord.Features.Alerts.Dust;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.Dust;

public sealed class DustConfigValidatorTests
{
    private readonly DustConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new DustConfig { Thresholds = DustConfig.DefaultThresholds }).Succeeded);
    }

    [Fact]
    public void Ascending_thresholds_are_accepted()
    {
        Assert.True(_validator.Validate(null, new DustConfig { Thresholds = [40, 120, 400] }).Succeeded);
    }

    [Fact]
    public void Descending_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new DustConfig { Thresholds = [500, 150, 50] });

        Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Fewer_than_three_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new DustConfig { Thresholds = [50, 150] });

        Assert.Contains("needs exactly three values", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Equal_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new DustConfig { Thresholds = [50, 50, 500] });

        Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_window_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new DustConfig { Thresholds = DustConfig.DefaultThresholds, WindowHours = 0 });

        Assert.Contains("WindowHours", ValidationAssert.SingleFailure(result));
    }
}
