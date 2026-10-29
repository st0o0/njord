using Njord.Features.Alerts.HeavyRain;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.HeavyRain;

public sealed class HeavyRainConfigValidatorTests
{
    private readonly HeavyRainConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new HeavyRainConfig()).Succeeded);
    }

    [Fact]
    public void A_window_hours_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new HeavyRainConfig { WindowHours = 0 });

        Assert.Contains("WindowHours must be at least 1", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_non_positive_hourly_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new HeavyRainConfig { HourlyThreshold = 0 });

        Assert.Contains("HourlyThreshold must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_daily_threshold_not_exceeding_hourly_is_rejected()
    {
        var result = _validator.Validate(null, new HeavyRainConfig { HourlyThreshold = 10, DailyThreshold = 10 });

        Assert.Contains("DailyThreshold (10) must be greater than HourlyThreshold (10)", ValidationAssert.SingleFailure(result));
    }
}
