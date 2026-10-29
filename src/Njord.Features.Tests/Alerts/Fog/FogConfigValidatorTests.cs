using Njord.Features.Alerts.Fog;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.Fog;

public sealed class FogConfigValidatorTests
{
    private readonly FogConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new FogConfig()).Succeeded);
    }

    [Fact]
    public void A_window_hours_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new FogConfig { WindowHours = 0, PersistentHours = 0 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("WindowHours must be at least 1"));
    }

    [Fact]
    public void A_persistent_hours_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new FogConfig { PersistentHours = 0 });

        Assert.Contains("PersistentHours must be at least 1", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_persistent_hours_exceeding_window_hours_is_rejected()
    {
        var result = _validator.Validate(null, new FogConfig { WindowHours = 6, PersistentHours = 8 });

        Assert.Contains("PersistentHours (8) must not exceed WindowHours (6)", ValidationAssert.SingleFailure(result));
    }
}
