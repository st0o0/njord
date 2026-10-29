using Njord.Features.Conditions.OceanCurrents;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Conditions.OceanCurrents;

public sealed class OceanCurrentsConfigValidatorTests
{
    private readonly OceanCurrentsConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new OceanCurrentsConfig()).Succeeded);
    }

    [Fact]
    public void A_zero_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new OceanCurrentsConfig { StrongCurrentThresholdKmh = 0 });

        Assert.Contains("StrongCurrentThresholdKmh must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_negative_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new OceanCurrentsConfig { StrongCurrentThresholdKmh = -1 });

        Assert.Contains("StrongCurrentThresholdKmh must be positive", ValidationAssert.SingleFailure(result));
    }
}
