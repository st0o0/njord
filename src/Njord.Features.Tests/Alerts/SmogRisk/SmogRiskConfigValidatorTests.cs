using Njord.Features.Alerts.SmogRisk;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.SmogRisk;

public sealed class SmogRiskConfigValidatorTests
{
    private readonly SmogRiskConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new SmogRiskConfig()).Succeeded);
    }

    [Fact]
    public void A_non_positive_boundary_layer_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new SmogRiskConfig { BoundaryLayerThresholdM = 0 });

        Assert.Contains("BoundaryLayerThresholdM must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_non_positive_pm25_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new SmogRiskConfig { Pm25ThresholdUgm3 = 0 });

        Assert.Contains("Pm25ThresholdUgm3 must be positive", ValidationAssert.SingleFailure(result));
    }
}
