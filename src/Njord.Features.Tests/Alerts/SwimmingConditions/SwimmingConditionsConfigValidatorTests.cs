using Njord.Features.Alerts.SwimmingConditions;

namespace Njord.Features.Tests.Alerts.SwimmingConditions;

public sealed class SwimmingConditionsConfigValidatorTests
{
    private readonly SwimmingConditionsConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid() => Assert.True(_validator.Validate(null, new SwimmingConditionsConfig()).Succeeded);

    [Fact]
    public void A_window_hours_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new SwimmingConditionsConfig { WindowHours = 0 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("WindowHours must be at least 1"));
    }

    [Fact]
    public void Wave_height_ordering_is_enforced()
    {
        var result = _validator.Validate(null, new SwimmingConditionsConfig
        {
            DangerousWaveHeightM = 0.5,
            ModerateWaveHeightM = 1.0,
        });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("DangerousWaveHeightM"));
    }

    [Fact]
    public void Water_temp_ordering_is_enforced()
    {
        var result = _validator.Validate(null, new SwimmingConditionsConfig
        {
            DangerousWaterTempCelsius = 20.0,
            ModerateWaterTempCelsius = 16.0,
        });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("DangerousWaterTempCelsius"));
    }
}
