using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class SatelliteOptionsSpec
{
    [Fact]
    public void Should_default_to_6_past_hours()
    {
        var options = new SatelliteOptions();

        Assert.Multiple(
            () => Assert.Equal(6, options.PastHours),
            () => Assert.True(new SatelliteOptionsValidator().Validate(null, options).Succeeded));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(48)]
    public void Should_accept_past_hours_within_the_limits(int hours)
    {
        var result = new SatelliteOptionsValidator().Validate(null, new SatelliteOptions { PastHours = hours });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(49)]
    public void Should_reject_past_hours_outside_one_to_48(int hours)
    {
        var result = new SatelliteOptionsValidator().Validate(null, new SatelliteOptions { PastHours = hours });

        Assert.Equal("PastHours must be between 1 and 48.", ValidationAssert.SingleFailure(result));
    }
}
