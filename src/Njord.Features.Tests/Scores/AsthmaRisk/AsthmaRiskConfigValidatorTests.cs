using Njord.Features.Scores.AsthmaRisk;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Scores.AsthmaRisk;

public sealed class AsthmaRiskConfigValidatorTests
{
    private readonly AsthmaRiskConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid() => Assert.True(_validator.Validate(null, new AsthmaRiskConfig()).Succeeded);

    [Fact]
    public void Weights_not_summing_to_100_are_rejected()
    {
        var config = new AsthmaRiskConfig { Pm25Weight = 50 };

        var result = _validator.Validate(null, config);

        Assert.Contains("weights must sum to 100", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_negative_weight_is_rejected()
    {
        var config = new AsthmaRiskConfig { Pm25Weight = -5, OzoneWeight = 40 };

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("Pm25Weight must not be negative"));
    }

    [Theory]
    [InlineData(nameof(AsthmaRiskConfig.MinWindowScore))]
    [InlineData(nameof(AsthmaRiskConfig.MinWindowHours))]
    [InlineData(nameof(AsthmaRiskConfig.MaxWindows))]
    public void A_window_param_below_one_is_rejected(string property)
    {
        var config = new AsthmaRiskConfig();
        typeof(AsthmaRiskConfig).GetProperty(property)!.SetValue(config, 0);

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains($"{property} must be at least 1"));
    }

    [Fact]
    public void Zero_pm25_reference_is_rejected()
    {
        var result = _validator.Validate(null, new AsthmaRiskConfig { Pm25Reference = 0 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("Pm25Reference must be positive"));
    }

    [Fact]
    public void Cold_air_factor_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new AsthmaRiskConfig { ColdAirFactor = 0.5 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("ColdAirFactor must be at least 1.0"));
    }

    [Fact]
    public void Zero_rapid_change_degrees_is_rejected()
    {
        var result = _validator.Validate(null, new AsthmaRiskConfig { RapidChangeDegrees = 0 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("RapidChangeDegrees must be positive"));
    }
}
