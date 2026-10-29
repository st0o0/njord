using Njord.Features.Alerts.Ice;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.Ice;

public sealed class IceConfigValidatorTests
{
    private readonly IceConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new IceConfig()).Succeeded);
    }

    [Fact]
    public void A_window_hours_below_one_is_rejected()
    {
        var result = _validator.Validate(null, new IceConfig { WindowHours = 0 });

        Assert.Contains("WindowHours must be at least 1", ValidationAssert.SingleFailure(result));
    }
}
