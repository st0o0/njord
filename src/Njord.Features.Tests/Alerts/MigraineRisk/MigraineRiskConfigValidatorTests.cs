using Njord.Features.Alerts.MigraineRisk;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.MigraineRisk;

public sealed class MigraineRiskConfigValidatorTests
{
    private readonly MigraineRiskConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new MigraineRiskConfig()).Succeeded);
    }

    [Fact]
    public void A_non_positive_pressure_6h_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new MigraineRiskConfig { PressureThreshold6h = 0 });

        Assert.Contains("PressureThreshold6h must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_non_positive_pressure_24h_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new MigraineRiskConfig { PressureThreshold24h = 0 });

        Assert.Contains("PressureThreshold24h must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_non_positive_temp_swing_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new MigraineRiskConfig { TempSwingThreshold = 0 });

        Assert.Contains("TempSwingThreshold must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_non_positive_humidity_swing_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new MigraineRiskConfig { HumiditySwingThreshold = 0 });

        Assert.Contains("HumiditySwingThreshold must be positive", ValidationAssert.SingleFailure(result));
    }
}
