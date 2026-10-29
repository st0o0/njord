using Njord.Features.Conditions.SolarYield;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Conditions.SolarYield;

public sealed class SolarYieldConfigValidatorTests
{
    private readonly SolarYieldConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid_with_no_battery()
    {
        var config = new SolarYieldConfig();

        Assert.Multiple(
            () => Assert.True(_validator.Validate(null, config).Succeeded),
            () => Assert.Equal(5.0, config.PeakKw),
            () => Assert.Equal(0.8, config.SystemEfficiency),
            () => Assert.Equal(0.0, config.BatteryCapacityKwh));
    }

    [Fact]
    public void A_non_positive_peak_is_rejected()
    {
        var result = _validator.Validate(null, new SolarYieldConfig { PeakKw = 0 });

        Assert.Contains("PeakKw must be positive", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.5)]
    public void An_efficiency_outside_zero_exclusive_to_one_is_rejected(double efficiency)
    {
        var result = _validator.Validate(null, new SolarYieldConfig { SystemEfficiency = efficiency });

        Assert.Contains("SystemEfficiency must be greater than 0 and at most 1", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void An_efficiency_of_exactly_one_is_accepted()
    {
        Assert.True(_validator.Validate(null, new SolarYieldConfig { SystemEfficiency = 1.0 }).Succeeded);
    }

    [Fact]
    public void A_negative_battery_capacity_is_rejected()
    {
        var result = _validator.Validate(null, new SolarYieldConfig { BatteryCapacityKwh = -1 });

        Assert.Contains("BatteryCapacityKwh must not be negative", ValidationAssert.SingleFailure(result));
    }
}
