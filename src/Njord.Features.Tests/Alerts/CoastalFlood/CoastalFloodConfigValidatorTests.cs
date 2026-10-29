using Njord.Features.Alerts.CoastalFlood;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts.CoastalFlood;

public sealed class CoastalFloodConfigValidatorTests
{
    private readonly CoastalFloodConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new CoastalFloodConfig()).Succeeded);
    }

    [Fact]
    public void A_non_positive_sea_level_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new CoastalFloodConfig { SeaLevelThresholdM = 0 });

        Assert.Contains("SeaLevelThresholdM must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_non_positive_wave_height_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new CoastalFloodConfig { WaveHeightThresholdM = -1 });

        Assert.Contains("WaveHeightThresholdM must be positive", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_non_positive_wind_speed_threshold_is_rejected()
    {
        var result = _validator.Validate(null, new CoastalFloodConfig { WindSpeedThreshold = 0 });

        Assert.Contains("WindSpeedThreshold must be positive", ValidationAssert.SingleFailure(result));
    }
}
