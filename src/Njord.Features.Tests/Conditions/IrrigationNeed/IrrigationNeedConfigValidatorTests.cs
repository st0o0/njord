using Njord.Features.Conditions.IrrigationNeed;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Conditions.IrrigationNeed;

public sealed class IrrigationNeedConfigValidatorTests
{
    private readonly IrrigationNeedConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new IrrigationNeedConfig()).Succeeded);
    }

    [Fact]
    public void Inverted_thresholds_are_rejected()
    {
        var result = _validator.Validate(null, new IrrigationNeedConfig { DryThresholdPercent = 70, WetThresholdPercent = 30 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("DryThresholdPercent must be less than WetThresholdPercent"));
    }

    [Fact]
    public void An_out_of_range_dry_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new IrrigationNeedConfig { DryThresholdPercent = 101 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("DryThresholdPercent must be 0..100"));
    }

    [Fact]
    public void A_negative_wet_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new IrrigationNeedConfig { WetThresholdPercent = -5 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("WetThresholdPercent must be 0..100"));
    }
}
