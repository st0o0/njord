using Njord.Features.Conditions.SoilMoisture;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Conditions.SoilMoisture;

public sealed class SoilMoistureConfigValidatorTests
{
    private readonly SoilMoistureConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new SoilMoistureConfig()).Succeeded);
    }

    [Fact]
    public void A_zero_dry_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new SoilMoistureConfig { DryThreshold = 0 });

        Assert.Contains("DryThreshold must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_zero_wet_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new SoilMoistureConfig { WetThreshold = 0 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("WetThreshold must be positive"));
    }

    [Fact]
    public void Inverted_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new SoilMoistureConfig { DryThreshold = 0.5, WetThreshold = 0.2 });

        Assert.Contains("DryThreshold", ValidationAssert.SingleFailure(result));
        Assert.Contains("less than WetThreshold", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Equal_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new SoilMoistureConfig { DryThreshold = 0.3, WetThreshold = 0.3 });

        Assert.Contains("less than WetThreshold", ValidationAssert.SingleFailure(result));
    }
}
