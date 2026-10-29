using Njord.Features.Conditions.HeatPumpCop;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Conditions.HeatPumpCop;

public sealed class HeatPumpCopConfigValidatorTests
{
    private readonly HeatPumpCopConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new HeatPumpCopConfig()).Succeeded);
    }

    [Fact]
    public void A_zero_forecast_hours_is_rejected()
    {
        var result = _validator.Validate(null, new HeatPumpCopConfig { ForecastHours = 0 });

        Assert.Contains("ForecastHours must be at least 1", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_negative_forecast_hours_is_rejected()
    {
        var result = _validator.Validate(null, new HeatPumpCopConfig { ForecastHours = -1 });

        Assert.Contains("ForecastHours must be at least 1", ValidationAssert.SingleFailure(result));
    }
}
