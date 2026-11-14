using Njord.Features.Conditions.ApproachingFront;

namespace Njord.Features.Tests.Conditions.ApproachingFront;

public sealed class ApproachingFrontConfigValidatorTests
{
    private readonly ApproachingFrontConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid() => Assert.True(_validator.Validate(null, new ApproachingFrontConfig()).Succeeded);

    [Fact]
    public void A_window_hours_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new ApproachingFrontConfig { WindowHours = 0 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("WindowHours must be at least 1"));
    }
}
