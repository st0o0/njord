using Njord.Core.Configuration;
using Njord.OpenMeteo.Configuration;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests.Configuration;

public sealed class SolarYieldOptionsValidatorTests
{
    private readonly SolarYieldOptionsValidator _validator = new();

    [Fact]
    public void Should_accept_the_default_options()
    {
        Assert.True(_validator.Validate(null, new SolarYieldOptions()).Succeeded);
    }

    [Fact]
    public void Should_reject_a_non_positive_peak()
    {
        var result = _validator.Validate(null, new SolarYieldOptions { PeakKw = 0 });

        Assert.Contains("SolarYield.PeakKw must be positive", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1.1)]
    public void Should_reject_a_system_efficiency_outside_zero_to_one(double efficiency)
    {
        var result = _validator.Validate(null, new SolarYieldOptions { SystemEfficiency = efficiency });

        Assert.Contains("SolarYield.SystemEfficiency must be greater than 0", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(361)]
    public void Should_reject_an_azimuth_outside_zero_to_360(int azimuth)
    {
        var result = _validator.Validate(null, new SolarYieldOptions { Azimuth = azimuth });

        Assert.Contains("SolarYield.Azimuth must be between 0 and 360", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(91)]
    public void Should_reject_a_tilt_outside_zero_to_90(int tilt)
    {
        var result = _validator.Validate(null, new SolarYieldOptions { Tilt = tilt });

        Assert.Contains("SolarYield.Tilt must be between 0 and 90", ValidationAssert.SingleFailure(result));
    }
}
