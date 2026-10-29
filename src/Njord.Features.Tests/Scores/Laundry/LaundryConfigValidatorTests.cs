using Njord.Features.Scores.Laundry;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Scores.Laundry;

public sealed class LaundryConfigValidatorTests
{
    private readonly LaundryConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new LaundryConfig()).Succeeded);
    }

    [Theory]
    [InlineData(nameof(LaundryConfig.MinWindowScore))]
    [InlineData(nameof(LaundryConfig.MinWindowHours))]
    [InlineData(nameof(LaundryConfig.MaxWindows))]
    public void A_window_param_below_one_is_rejected(string property)
    {
        var config = new LaundryConfig();
        typeof(LaundryConfig).GetProperty(property)!.SetValue(config, 0);

        var result = _validator.Validate(null, config);

        Assert.Contains($"{property} must be at least 1", ValidationAssert.SingleFailure(result));
    }
}
