using Njord.Core.Configuration;
using Njord.OpenMeteo.Configuration;
using Njord.Tests.Shared;

namespace Njord.OpenMeteo.Tests.Configuration;

public sealed class SatelliteOptionsValidatorTests
{
    private readonly SatelliteOptionsValidator _validator = new();

    [Fact]
    public void Should_accept_the_default_options()
    {
        Assert.True(_validator.Validate(null, new SatelliteOptions()).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(49)]
    public void Should_reject_past_hours_outside_one_to_48(int value)
    {
        var result = _validator.Validate(null, new SatelliteOptions { PastHours = value });

        Assert.Contains("Satellite.PastHours must be between 1 and 48", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_default_to_tilt_30_and_azimuth_180()
    {
        var options = new SatelliteOptions();

        Assert.Multiple(
            () => Assert.Equal(30, options.Tilt),
            () => Assert.Equal(180, options.Azimuth));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    public void Should_accept_tilt_at_the_boundaries(int value)
    {
        Assert.True(_validator.Validate(null, new SatelliteOptions { Tilt = value }).Succeeded);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(91)]
    public void Should_reject_tilt_outside_zero_to_90(int value)
    {
        var result = _validator.Validate(null, new SatelliteOptions { Tilt = value });

        Assert.Contains("Satellite.Tilt must be between 0 and 90", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(360)]
    public void Should_accept_azimuth_at_the_boundaries(int value)
    {
        Assert.True(_validator.Validate(null, new SatelliteOptions { Azimuth = value }).Succeeded);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(361)]
    public void Should_reject_azimuth_outside_zero_to_360(int value)
    {
        var result = _validator.Validate(null, new SatelliteOptions { Azimuth = value });

        Assert.Contains("Satellite.Azimuth must be between 0 and 360", ValidationAssert.SingleFailure(result));
    }
}
