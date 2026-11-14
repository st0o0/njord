using Njord.Features.Scores.SprayWindow;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Scores.SprayWindow;

public sealed class SprayWindowConfigValidatorTests
{
    private readonly SprayWindowConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid() => Assert.True(_validator.Validate(null, new SprayWindowConfig()).Succeeded);

    [Fact]
    public void A_non_positive_max_wind_speed_is_rejected()
    {
        var result = _validator.Validate(null, new SprayWindowConfig { MaxWindSpeed = 0 });

        Assert.Contains("MaxWindSpeed must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Rain_free_hours_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new SprayWindowConfig { RainFreeHours = 0 });

        Assert.Contains("RainFreeHours must be at least 1", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Min_temp_not_less_than_max_temp_is_rejected()
    {
        var result = _validator.Validate(null, new SprayWindowConfig { MinTemp = 25, MaxTemp = 25 });

        Assert.Contains("MinTemp must be less than MaxTemp", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(nameof(SprayWindowConfig.MinWindowScore))]
    [InlineData(nameof(SprayWindowConfig.MinWindowHours))]
    [InlineData(nameof(SprayWindowConfig.MaxWindows))]
    public void A_window_param_below_one_is_rejected(string property)
    {
        var config = new SprayWindowConfig();
        typeof(SprayWindowConfig).GetProperty(property)!.SetValue(config, 0);

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains($"{property} must be at least 1"));
    }

    [Fact]
    public void Rain_probability_limit_above_100_is_rejected()
    {
        var result = _validator.Validate(null, new SprayWindowConfig { RainProbabilityLimit = 101 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("RainProbabilityLimit must be 0..100"));
    }

    [Fact]
    public void Inverted_humidity_range_is_rejected()
    {
        var result = _validator.Validate(null, new SprayWindowConfig { HumidityMin = 80.0, HumidityMax = 40.0 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("HumidityMin must be less than HumidityMax"));
    }

    [Fact]
    public void Zero_falloff_is_rejected()
    {
        var result = _validator.Validate(null, new SprayWindowConfig { TemperatureFalloff = 0 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("TemperatureFalloff must be positive"));
    }
}
