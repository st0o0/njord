using Njord.Features.Conditions.FrostDepth;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Conditions.FrostDepth;

public sealed class FrostDepthConfigValidatorTests
{
    private readonly FrostDepthConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new FrostDepthConfig()).Succeeded);
    }

    [Fact]
    public void A_zero_pipe_depth_is_rejected()
    {
        var result = _validator.Validate(null, new FrostDepthConfig { PipeDepthCm = 0 });

        Assert.Contains("PipeDepthCm must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_negative_pipe_depth_is_rejected()
    {
        var result = _validator.Validate(null, new FrostDepthConfig { PipeDepthCm = -10 });

        Assert.Contains("PipeDepthCm must be positive", ValidationAssert.SingleFailure(result));
    }
}
