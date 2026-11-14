using Njord.Features.Conditions.SurfConditions;

namespace Njord.Features.Tests.Conditions.SurfConditions;

public sealed class SurfConditionsConfigValidatorTests
{
    private readonly SurfConditionsConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid() => Assert.True(_validator.Validate(null, new SurfConditionsConfig()).Succeeded);

    [Fact]
    public void Window_hours_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new SurfConditionsConfig { WindowHours = 0 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("WindowHours must be at least 1"));
    }

    [Fact]
    public void Inverted_period_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new SurfConditionsConfig { MediumPeriodSeconds = 15.0, LongPeriodSeconds = 10.0 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("MediumPeriodSeconds"));
    }

    [Fact]
    public void Inverted_height_range_is_rejected()
    {
        var result = _validator.Validate(null, new SurfConditionsConfig { IdealHeightMinMeters = 3.0, IdealHeightMaxMeters = 2.0 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("IdealHeightMinMeters"));
    }

    [Fact]
    public void Inverted_choppy_shares_are_rejected()
    {
        var result = _validator.Validate(null, new SurfConditionsConfig { ChoppyShare = 0.8, VeryChoppyShare = 0.5 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("ChoppyShare must be less than VeryChoppyShare"));
    }
}
