using Njord.Features.Conditions.Evapotranspiration;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Conditions.Evapotranspiration;

public sealed class EvapotranspirationConfigValidatorTests
{
    private readonly EvapotranspirationConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new EvapotranspirationConfig()).Succeeded);
    }

    [Fact]
    public void Low_not_less_than_high_is_rejected()
    {
        var result = _validator.Validate(null, new EvapotranspirationConfig { LowMm = 5, HighMm = 5 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("LowMm must be less than HighMm"));
    }

    [Fact]
    public void A_non_positive_low_is_rejected()
    {
        var result = _validator.Validate(null, new EvapotranspirationConfig { LowMm = 0 });

        Assert.Contains("LowMm must be positive", ValidationAssert.SingleFailure(result));
    }
}
