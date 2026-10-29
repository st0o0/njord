using Njord.Features.Alerts.Humidity;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.Humidity;

public sealed class HumidityConfigValidatorTests
{
    private readonly HumidityConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new HumidityConfig { Thresholds = HumidityConfig.DefaultThresholds }).Succeeded);
    }

    [Fact]
    public void Ascending_thresholds_are_accepted()
    {
        Assert.True(_validator.Validate(null, new HumidityConfig { Thresholds = [14, 19, 22] }).Succeeded);
    }

    [Fact]
    public void Descending_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new HumidityConfig { Thresholds = [24, 21, 16] });

        Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Fewer_than_three_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new HumidityConfig { Thresholds = [16, 21] });

        Assert.Contains("needs exactly three values", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Equal_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new HumidityConfig { Thresholds = [16, 16, 24] });

        Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_window_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new HumidityConfig { Thresholds = HumidityConfig.DefaultThresholds, WindowHours = 0 });

        Assert.Contains("WindowHours", ValidationAssert.SingleFailure(result));
    }
}
