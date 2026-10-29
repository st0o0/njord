using Njord.Features.Conditions.SnowDepthWatch;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Conditions.SnowDepthWatch;

public sealed class SnowDepthWatchConfigValidatorTests
{
    private readonly SnowDepthWatchConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new SnowDepthWatchConfig()).Succeeded);
    }

    [Fact]
    public void Non_ascending_thresholds_are_rejected()
    {
        var config = new SnowDepthWatchConfig { LightThreshold = 10, ModerateThreshold = 5 };

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("strictly ascending"));
    }

    [Fact]
    public void A_zero_threshold_is_rejected()
    {
        var config = new SnowDepthWatchConfig { LightThreshold = 0 };

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("LightThreshold must be positive"));
    }
}
