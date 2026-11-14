using Njord.Features.Scores.Laundry;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Scores.Laundry;

public sealed class LaundryConfigValidatorTests
{
    private readonly LaundryConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid() => Assert.True(_validator.Validate(null, new LaundryConfig()).Succeeded);

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

    [Fact]
    public void Weights_not_summing_to_one_are_rejected()
    {
        var result = _validator.Validate(null, new LaundryConfig { TemperatureWeight = 0.5 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("weights must sum to 1.0"));
    }

    [Fact]
    public void Inverted_temperature_range_is_rejected()
    {
        var result = _validator.Validate(null, new LaundryConfig { TemperatureLow = 30.0, TemperatureHigh = 10.0 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("TemperatureLow"));
    }

    [Fact]
    public void Zero_good_hour_score_is_rejected()
    {
        var result = _validator.Validate(null, new LaundryConfig { GoodHourScore = 0 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("GoodHourScore"));
    }
}
