using Njord.Features.Conditions.ExerciseAdvisory;

namespace Njord.Features.Tests.Conditions.ExerciseAdvisory;

public sealed class ExerciseAdvisoryConfigValidatorTests
{
    private readonly ExerciseAdvisoryConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid() => Assert.True(_validator.Validate(null, new ExerciseAdvisoryConfig()).Succeeded);

    [Fact]
    public void Weights_not_summing_to_one_are_rejected()
    {
        var result = _validator.Validate(null, new ExerciseAdvisoryConfig { Pm25Weight = 0.5 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("weights must sum to 1.0"));
    }

    [Fact]
    public void Safe_score_below_caution_score_is_rejected()
    {
        var result = _validator.Validate(null, new ExerciseAdvisoryConfig { SafeScore = 30.0, CautionScore = 45.0 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("SafeScore must be greater than CautionScore"));
    }

    [Fact]
    public void Negative_safe_score_is_rejected()
    {
        var result = _validator.Validate(null, new ExerciseAdvisoryConfig { SafeScore = -1.0 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("SafeScore must be positive"));
    }

    [Fact]
    public void Inverted_temperature_band_is_rejected()
    {
        var result = _validator.Validate(null, new ExerciseAdvisoryConfig { TemperatureCool = 25.0, TemperatureWarm = 20.0 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("TemperatureCool"));
    }
}
