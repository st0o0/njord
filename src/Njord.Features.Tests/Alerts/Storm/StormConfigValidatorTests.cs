using Njord.Features.Alerts.Storm;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.Storm;

public sealed class StormConfigValidatorTests
{
    private readonly StormConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new StormConfig { GustThresholds = StormConfig.DefaultGustThresholds }).Succeeded);
    }

    [Fact]
    public void Ascending_thresholds_are_accepted()
    {
        Assert.True(_validator.Validate(null, new StormConfig { GustThresholds = [15, 22, 30] }).Succeeded);
    }

    [Fact]
    public void Descending_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new StormConfig { GustThresholds = [33, 25, 17] });

        Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Fewer_than_three_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new StormConfig { GustThresholds = [17, 25] });

        Assert.Contains("needs exactly three values", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Equal_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new StormConfig { GustThresholds = [17, 17, 33] });

        Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_window_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new StormConfig { GustThresholds = StormConfig.DefaultGustThresholds, WindowHours = 0 });

        Assert.Contains("WindowHours", ValidationAssert.SingleFailure(result));
    }
}
