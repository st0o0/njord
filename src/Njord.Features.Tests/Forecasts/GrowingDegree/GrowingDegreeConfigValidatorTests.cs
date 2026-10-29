using Njord.Features.Forecasts.GrowingDegree;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Forecasts.GrowingDegree;

public sealed class GrowingDegreeConfigValidatorTests
{
    private readonly GrowingDegreeConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new GrowingDegreeConfig()).Succeeded);
    }

    [Fact]
    public void A_temperature_below_minus_ten_is_rejected()
    {
        var result = _validator.Validate(null, new GrowingDegreeConfig { BaseTemperature = -11 });

        Assert.Contains("BaseTemperature must be between -10 and 30", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_temperature_above_thirty_is_rejected()
    {
        var result = _validator.Validate(null, new GrowingDegreeConfig { BaseTemperature = 31 });

        Assert.Contains("BaseTemperature must be between -10 and 30", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Boundary_values_are_accepted()
    {
        Assert.Multiple(
            () => Assert.True(_validator.Validate(null, new GrowingDegreeConfig { BaseTemperature = -10 }).Succeeded),
            () => Assert.True(_validator.Validate(null, new GrowingDegreeConfig { BaseTemperature = 30 }).Succeeded));
    }
}
