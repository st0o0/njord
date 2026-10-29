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
}
