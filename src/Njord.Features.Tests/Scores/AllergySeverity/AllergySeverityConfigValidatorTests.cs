using Njord.Features.Scores.AllergySeverity;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Scores.AllergySeverity;

public sealed class AllergySeverityConfigValidatorTests
{
    private readonly AllergySeverityConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid() => Assert.True(_validator.Validate(null, new AllergySeverityConfig()).Succeeded);

    [Theory]
    [InlineData(nameof(AllergySeverityConfig.MinWindowScore))]
    [InlineData(nameof(AllergySeverityConfig.MinWindowHours))]
    [InlineData(nameof(AllergySeverityConfig.MaxWindows))]
    public void A_window_param_below_one_is_rejected(string property)
    {
        var config = new AllergySeverityConfig();
        typeof(AllergySeverityConfig).GetProperty(property)!.SetValue(config, 0);

        var result = _validator.Validate(null, config);

        Assert.Contains($"{property} must be at least 1", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Zero_pollen_reference_is_rejected()
    {
        var result = _validator.Validate(null, new AllergySeverityConfig { PollenReference = 0 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("PollenReference must be positive"));
    }

    [Fact]
    public void Zero_good_hour_score_is_rejected()
    {
        var result = _validator.Validate(null, new AllergySeverityConfig { GoodHourScore = 0 });
        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("GoodHourScore must be positive"));
    }
}
