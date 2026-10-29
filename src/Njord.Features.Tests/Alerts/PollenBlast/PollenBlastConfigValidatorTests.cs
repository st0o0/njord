using Njord.Features.Alerts.PollenBlast;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.PollenBlast;

public sealed class PollenBlastConfigValidatorTests
{
    private readonly PollenBlastConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new PollenBlastConfig()).Succeeded);
    }

    [Fact]
    public void A_non_positive_temperature_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new PollenBlastConfig { TemperatureThreshold = 0 });

        Assert.Contains("TemperatureThreshold must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_non_positive_wind_speed_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new PollenBlastConfig { WindSpeedThreshold = 0 });

        Assert.Contains("WindSpeedThreshold must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_non_positive_pollen_delta_is_rejected()
    {
        var result = _validator.Validate(null, new PollenBlastConfig { PollenDelta = 0 });

        Assert.Contains("PollenDelta must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_non_positive_precipitation_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new PollenBlastConfig { PrecipitationThreshold = 0 });

        Assert.Contains("PrecipitationThreshold must be positive", ValidationAssert.SingleFailure(result));
    }
}
